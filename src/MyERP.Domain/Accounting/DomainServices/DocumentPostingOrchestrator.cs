using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Orchestrates the full posting pipeline when a transaction document is submitted:
/// 1. Validates accounting period is not closed
/// 2. GL Entry creation (via AccountingRuleEngine)
/// 3. Payment Ledger Entry creation (for outstanding tracking)
/// 4. JournalEntry persistence
/// 
/// This is the single entry point for document posting — AppServices call this,
/// never the individual sub-services directly.
/// </summary>
public class DocumentPostingOrchestrator : DomainService
{
    private readonly AccountingRuleEngine _ruleEngine;
    private readonly PaymentLedgerService _pleService;
    private readonly BudgetValidationService _budgetValidationService;
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;
    private readonly IRepository<AccountingPeriod, Guid> _periodRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public DocumentPostingOrchestrator(
        AccountingRuleEngine ruleEngine,
        PaymentLedgerService pleService,
        BudgetValidationService budgetValidationService,
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<PaymentLedgerEntry, Guid> pleRepository,
        IRepository<AccountingPeriod, Guid> periodRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _ruleEngine = ruleEngine;
        _pleService = pleService;
        _budgetValidationService = budgetValidationService;
        _journalRepository = journalRepository;
        _pleRepository = pleRepository;
        _periodRepository = periodRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Post a Sales Invoice: creates GL entries + PLE (DR outstanding).
    /// Supports multi-currency: amountInAccountCurrency is in transaction currency.
    /// </summary>
    public async Task<JournalEntry> PostSalesInvoiceAsync(
        IAccountableDocument invoice,
        Guid receivableAccountId,
        DateTime? dueDate = null,
        string accountCurrency = "MYR",
        decimal exchangeRate = 1m)
    {
        await ValidatePostingPeriodAsync(invoice.CompanyId, invoice.PostingDate, invoice.DocumentType);

        // Step 1: Create GL entries via rule engine
        var journal = await _ruleEngine.PostDocumentAsync(invoice);
        await _journalRepository.InsertAsync(journal);

        // Step 2: Create PLE entry — DR (increases outstanding for customer)
        if (invoice.CustomerId.HasValue)
        {
            var baseAmount = Math.Round(invoice.GrandTotal * exchangeRate, 2);
            await _pleService.CreateEntryAsync(
                companyId: invoice.CompanyId,
                postingDate: invoice.PostingDate,
                accountId: receivableAccountId,
                partyType: "Customer",
                partyId: invoice.CustomerId.Value,
                voucherType: "SalesInvoice",
                voucherId: invoice.Id,
                againstVoucherType: "SalesInvoice",
                againstVoucherId: invoice.Id,
                amount: baseAmount,
                amountInAccountCurrency: invoice.GrandTotal,
                accountCurrency: accountCurrency,
                dueDate: dueDate);
        }

        return journal;
    }

    /// <summary>
    /// Post a Purchase Invoice: creates GL entries + PLE (CR outstanding).
    /// Supports multi-currency: amountInAccountCurrency is in transaction currency.
    /// </summary>
    public async Task<JournalEntry> PostPurchaseInvoiceAsync(
        IAccountableDocument invoice,
        Guid payableAccountId,
        DateTime? dueDate = null,
        string accountCurrency = "MYR",
        decimal exchangeRate = 1m)
    {
        await ValidatePostingPeriodAsync(invoice.CompanyId, invoice.PostingDate, invoice.DocumentType);

        var journal = await _ruleEngine.PostDocumentAsync(invoice);
        await _journalRepository.InsertAsync(journal);

        if (invoice.SupplierId.HasValue)
        {
            var baseAmount = Math.Round(-invoice.GrandTotal * exchangeRate, 2);
            await _pleService.CreateEntryAsync(
                companyId: invoice.CompanyId,
                postingDate: invoice.PostingDate,
                accountId: payableAccountId,
                partyType: "Supplier",
                partyId: invoice.SupplierId.Value,
                voucherType: "PurchaseInvoice",
                voucherId: invoice.Id,
                againstVoucherType: "PurchaseInvoice",
                againstVoucherId: invoice.Id,
                amount: baseAmount, // CR = negative in PLE
                amountInAccountCurrency: -invoice.GrandTotal,
                accountCurrency: accountCurrency,
                dueDate: dueDate);
        }

        return journal;
    }

    /// <summary>
    /// Post a Payment Entry: creates GL entries + PLE to reduce outstanding on allocated invoices.
    /// Supports multi-currency: allocatedAmount is in transaction currency,
    /// base amounts are converted using the payment's exchange rate.
    /// </summary>
    public async Task<JournalEntry> PostPaymentEntryAsync(
        IAccountableDocument payment,
        Guid partyAccountId,
        string partyType,
        Guid partyId,
        string accountCurrency,
        decimal exchangeRate,
        PaymentAllocation[] allocations)
    {
        await ValidatePostingPeriodAsync(payment.CompanyId, payment.PostingDate, payment.DocumentType);

        var journal = await _ruleEngine.PostDocumentAsync(payment);
        await _journalRepository.InsertAsync(journal);

        // Create PLE entries for each allocation — reduces outstanding on the target invoice
        foreach (var alloc in allocations)
        {
            var sign = partyType == "Customer" ? -1m : 1m; // Receive = CR customer, Pay = DR supplier
            var amountInAccCurrency = sign * alloc.AllocatedAmount;
            var baseAmount = Math.Round(amountInAccCurrency * exchangeRate, 2);

            await _pleService.CreateEntryAsync(
                companyId: payment.CompanyId,
                postingDate: payment.PostingDate,
                accountId: partyAccountId,
                partyType: partyType,
                partyId: partyId,
                voucherType: "PaymentEntry",
                voucherId: payment.Id,
                againstVoucherType: alloc.VoucherType,
                againstVoucherId: alloc.VoucherId,
                amount: baseAmount,
                amountInAccountCurrency: amountInAccCurrency,
                accountCurrency: accountCurrency);
        }

        // Exchange gain/loss JE for multi-currency payments
        // Per ERPNext: when payment rate ≠ invoice rate, book the difference
        if (exchangeRate != 1m && allocations.Length > 0)
        {
            // Get source exchange rate from the allocation context
            // Gain/loss = allocatedAmount × (paymentRate - invoiceRate)
            // This is calculated at the AppService level and stored on PaymentEntry.ExchangeGainLoss
            // The JE for gain/loss would DR/CR Exchange Gain/Loss account
            // (actual JE creation deferred to AppService where invoice rate is available)
        }

        return journal;
    }

    /// <summary>
    /// Post a Delivery Note (perpetual inventory): creates GL entries for COGS.
    /// DR: Cost of Goods Sold, CR: Stock In Hand
    /// </summary>
    public async Task<JournalEntry> PostDeliveryNoteAsync(IAccountableDocument deliveryNote)
    {
        await ValidatePostingPeriodAsync(deliveryNote.CompanyId, deliveryNote.PostingDate, deliveryNote.DocumentType);

        var journal = await _ruleEngine.PostDocumentAsync(deliveryNote);
        await _journalRepository.InsertAsync(journal);
        return journal;
    }

    /// <summary>
    /// Post a Purchase Receipt (perpetual inventory): creates GL entries for stock received.
    /// DR: Stock In Hand, CR: Stock Received But Not Billed
    /// </summary>
    public async Task<JournalEntry> PostPurchaseReceiptAsync(IAccountableDocument purchaseReceipt)
    {
        await ValidatePostingPeriodAsync(purchaseReceipt.CompanyId, purchaseReceipt.PostingDate, purchaseReceipt.DocumentType);

        var journal = await _ruleEngine.PostDocumentAsync(purchaseReceipt);
        await _journalRepository.InsertAsync(journal);
        return journal;
    }

    /// <summary>
    /// Reverse all PLE entries for a cancelled document by creating reversal entries.
    /// </summary>
    public async Task ReversePleForDocumentAsync(string voucherType, Guid voucherId)
    {
        var query = await _pleRepository.GetQueryableAsync();
        var entries = query
            .Where(e => e.VoucherType == voucherType && e.VoucherId == voucherId && !e.IsReversal)
            .ToList();

        foreach (var entry in entries)
        {
            await _pleService.CreateReversalAsync(entry);
        }
    }

    /// <summary>
    /// Validates actual GL expense against budget (Level 3 enforcement).
    /// Call this when posting expense GL entries (SI/PI/JE with debit to expense accounts).
    /// </summary>
    public async Task ValidateBudgetOnPostingAsync(
        Guid companyId, DateTime postingDate,
        IEnumerable<BudgetCheckItem> expenseItems, Guid? tenantId)
    {
        // Resolve fiscal year for the posting date
        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var fiscalYear = fyQuery.FirstOrDefault(fy =>
            fy.CompanyId == companyId
            && fy.StartDate <= postingDate
            && fy.EndDate >= postingDate);

        if (fiscalYear == null) return; // No FY = no budget to check

        await _budgetValidationService.ValidateForActualExpenseAsync(
            companyId, fiscalYear.Id, postingDate, expenseItems, tenantId);
    }

    /// <summary>
    /// Validates that the posting date does not fall in a closed accounting period
    /// or before the company's accounts frozen date.
    /// </summary>
    private async Task ValidatePostingPeriodAsync(Guid companyId, DateTime postingDate, string documentType)
    {
        // Check accounts frozen date
        var company = await _companyRepository.GetAsync(companyId);
        if (company.AccountsFrozenTillDate.HasValue && postingDate <= company.AccountsFrozenTillDate.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AccountingPeriodClosed)
                .WithData("frozenTill", company.AccountsFrozenTillDate.Value.ToString("yyyy-MM-dd"))
                .WithData("postingDate", postingDate.ToString("yyyy-MM-dd"));
        }

        // Check fiscal year exists and is open for the posting date
        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var fiscalYear = fyQuery.FirstOrDefault(fy =>
            fy.CompanyId == companyId
            && fy.StartDate <= postingDate
            && fy.EndDate >= postingDate);

        if (fiscalYear == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("postingDate", postingDate.ToString("yyyy-MM-dd"))
                .WithData("companyId", companyId);
        }

        if (fiscalYear.IsClosed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("postingDate", postingDate.ToString("yyyy-MM-dd"))
                .WithData("fiscalYear", fiscalYear.Name);
        }

        // Check accounting period closure
        var periodsQuery = await _periodRepository.GetQueryableAsync();
        var closedPeriod = periodsQuery
            .Where(p => p.IsClosed
                && p.StartDate <= postingDate
                && p.EndDate >= postingDate
                && p.CompanyId == companyId)
            .FirstOrDefault();

        if (closedPeriod != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AccountingPeriodClosed)
                .WithData("period", closedPeriod.PeriodName)
                .WithData("postingDate", postingDate.ToString("yyyy-MM-dd"));
        }
    }
}

/// <summary>Payment allocation against a specific invoice.</summary>
public class PaymentAllocation
{
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
    public decimal AllocatedAmount { get; set; }
}
