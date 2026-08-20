using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Payment Reconciliation AppService — matches unallocated payments to outstanding invoices.
/// Delegates to PaymentReconciliationEngine for business logic.
/// 
/// Per ERPNext: reconciliation of multi-currency payments MUST create exchange gain/loss JE.
/// Per DO-NOT: "Process Payment Reconciliation without exchange gain/loss JE for multi-currency differences"
/// Per DO-NOT: "Unreconcile without cancelling related exchange gain/loss Journal Entries"
/// </summary>
[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class PaymentReconciliationAppService : ApplicationService, IPaymentReconciliationAppService
{
    private readonly PaymentReconciliationEngine _engine;
    private readonly PaymentLedgerService _pleService;
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<PaymentEntry, Guid> _paymentEntryRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public PaymentReconciliationAppService(
        PaymentReconciliationEngine engine,
        PaymentLedgerService pleService,
        IRepository<PaymentLedgerEntry, Guid> pleRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<PaymentEntry, Guid> paymentEntryRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _engine = engine;
        _pleService = pleService;
        _pleRepository = pleRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _paymentEntryRepository = paymentEntryRepository;
        _journalEntryRepository = journalEntryRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Get all outstanding invoices for a party (for reconciliation UI).
    /// </summary>
    public async Task<List<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(string partyType, Guid partyId)
    {
        var vouchers = await _pleService.GetOutstandingVouchersAsync(partyType, partyId);
        return vouchers.Select(v => new OutstandingInvoiceDto
        {
            VoucherId = v.VoucherId,
            VoucherType = v.VoucherType,
            Outstanding = v.Outstanding,
        }).ToList();
    }

    /// <summary>
    /// Reconcile payments against invoices.
    /// Delegates to PaymentReconciliationEngine for stale-outstanding validation
    /// and batch processing.
    /// 
    /// Per ERPNext: when payment exchange rate differs from invoice exchange rate,
    /// creates an Exchange Gain/Loss JE to book the difference.
    /// gain_loss = allocated_amount × (payment_rate - invoice_rate)
    /// </summary>
    public async Task ReconcileAsync(ReconcilePaymentDto input)
    {
        // Resolve account currency from company (not hardcoded)
        var company = await _companyRepository.GetAsync(input.CompanyId);
        var accountCurrency = company.CurrencyCode;

        // Resolve party account
        var pleQuery = await _pleRepository.GetQueryableAsync();
        var partyAccount = pleQuery
            .Where(p => p.PartyType == input.PartyType && p.PartyId == input.PartyId)
            .Select(p => p.AccountId)
            .FirstOrDefault();

        // Build allocation list for engine
        var allocations = input.Allocations.Select(a => new ReconciliationAllocation
        {
            PaymentVoucherType = a.PaymentVoucherType,
            PaymentVoucherId = a.PaymentVoucherId,
            InvoiceVoucherType = a.InvoiceVoucherType,
            InvoiceVoucherId = a.InvoiceVoucherId,
            AllocatedAmount = a.AllocatedAmount,
        }).ToList();

        // Delegate to engine (handles stale-outstanding, batch, error isolation)
        var result = await _engine.ReconcileBatchAsync(
            input.CompanyId, input.PartyType, input.PartyId,
            partyAccount, accountCurrency, allocations);

        // Update invoice AmountPaid and create exchange gain/loss JE for successful allocations
        foreach (var alloc in input.Allocations)
        {
            // Skip allocations that failed in engine
            if (result.Errors.Any(e => e.InvoiceVoucherId == alloc.InvoiceVoucherId))
                continue;

            if (alloc.InvoiceVoucherType == "SalesInvoice")
            {
                await UpdateInvoiceAmountPaidAsync("SalesInvoice", alloc.InvoiceVoucherId, alloc.AllocatedAmount);

                // Exchange gain/loss JE for multi-currency reconciliation
                var si = await _salesInvoiceRepository.GetAsync(alloc.InvoiceVoucherId);
                await CreateExchangeGainLossJeIfNeeded(
                    company, alloc, si.ExchangeRate, input.PartyType, partyAccount);
            }
            else if (alloc.InvoiceVoucherType == "PurchaseInvoice")
            {
                await UpdateInvoiceAmountPaidAsync("PurchaseInvoice", alloc.InvoiceVoucherId, alloc.AllocatedAmount);

                // Exchange gain/loss JE for multi-currency reconciliation
                var pi = await _purchaseInvoiceRepository.GetAsync(alloc.InvoiceVoucherId);
                await CreateExchangeGainLossJeIfNeeded(
                    company, alloc, pi.ExchangeRate, input.PartyType, partyAccount);
            }
        }
    }

    /// <summary>
    /// Creates an Exchange Gain/Loss Journal Entry when payment rate != invoice rate.
    /// Per ERPNext: gain_loss = allocated_amount × (payment_rate - invoice_rate), with the sign
    /// reversed for Payable accounts (per payment-ledger-reconciliation skill's "PAYABLE ACCOUNT
    /// EXCEPTION" — the same rate difference means the opposite GL direction on a liability vs an
    /// asset account). Posts against the party's own receivable/payable account, not a placeholder —
    /// this reconciles the invoice's rate against the payment's rate directly, no bank movement
    /// happens here (unlike Payment Entry submit, which posts against the bank/cash account instead).
    /// </summary>
    private async Task CreateExchangeGainLossJeIfNeeded(
        Company company,
        ReconcileAllocationDto alloc,
        decimal invoiceExchangeRate,
        string partyType,
        Guid partyAccountId)
    {
        if (!company.ExchangeGainLossAccountId.HasValue) return;
        if (partyAccountId == Guid.Empty) return;

        // Get payment exchange rate
        decimal paymentExchangeRate = 1m;
        if (alloc.PaymentVoucherType == "PaymentEntry")
        {
            var pe = await _paymentEntryRepository.GetAsync(alloc.PaymentVoucherId);
            paymentExchangeRate = pe.ExchangeRate;
        }

        // Calculate gain/loss
        var gainLoss = PaymentReconciliationEngine.CalculateExchangeGainLoss(
            alloc.AllocatedAmount, paymentExchangeRate, invoiceExchangeRate);

        if (Math.Abs(gainLoss) < 0.01m) return; // No material difference

        // Payable accounts use the reversed sign convention for reconciliation exchange
        // differences (a rate increase that's a gain on a receivable is a loss on a payable).
        var effectiveGainLoss = partyType == "Supplier" ? -gainLoss : gainLoss;

        // Resolve fiscal year
        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var fy = fyQuery.FirstOrDefault(f =>
            f.CompanyId == company.Id && f.StartDate <= DateTime.UtcNow.Date && f.EndDate >= DateTime.UtcNow.Date);

        if (fy == null) return; // Cannot post without fiscal year

        // Create Exchange Gain/Loss JE — tagged with the actual payment voucher's type/id (not a
        // constant), so DocumentPostingOrchestrator.ReverseExchangeGainLossJournalEntriesAsync can
        // find and reverse it when that payment is unreconciled or cancelled.
        var je = new JournalEntry(
            GuidGenerator.Create(), company.Id, fy.Id, DateTime.UtcNow.Date)
        {
            VoucherType = JournalEntryVoucherType.ExchangeGainOrLoss,
            ReferenceType = alloc.PaymentVoucherType,
            ReferenceId = alloc.PaymentVoucherId,
        };
        je.Narration = $"Exchange {(effectiveGainLoss > 0 ? "Gain" : "Loss")} on reconciliation";

        var exchangeAccountId = company.ExchangeGainLossAccountId.Value;
        var absGainLoss = Math.Abs(effectiveGainLoss);

        if (effectiveGainLoss > 0)
        {
            // Gain: DR party account (asset up / liability down), CR Exchange Gain
            je.AddLine(partyAccountId, absGainLoss, true);
            je.AddLine(exchangeAccountId, absGainLoss, false);
        }
        else
        {
            // Loss: DR Exchange Loss, CR party account (asset down / liability up)
            je.AddLine(exchangeAccountId, absGainLoss, true);
            je.AddLine(partyAccountId, absGainLoss, false);
        }

        je.Post();
        await _journalEntryRepository.InsertAsync(je);
    }

    /// <summary>
    /// Unreconcile a previous payment-to-invoice allocation.
    /// Delegates to PaymentReconciliationEngine which handles delink + amount tracking.
    /// Per DO-NOT: "Unreconcile without cancelling related exchange gain/loss Journal Entries"
    /// </summary>
    public async Task UnreconcileAsync(UnreconcileDto input)
    {
        // Engine returns the allocated amount that was delinked
        var allocatedAmount = await _engine.UnreconcileAsync(
            input.PaymentVoucherType, input.PaymentVoucherId,
            input.InvoiceVoucherType, input.InvoiceVoucherId);

        // Reduce the invoice's AmountPaid (with concurrency retry)
        if (allocatedAmount > 0)
        {
            await UpdateInvoiceAmountPaidAsync(input.InvoiceVoucherType, input.InvoiceVoucherId, -allocatedAmount);
        }

        // Cancel related exchange gain/loss JEs
        // Per ERPNext: unreconciliation must cancel exchange gain/loss JE that was created during reconcile
        var jeQuery = await _journalEntryRepository.GetQueryableAsync();
        var relatedJes = jeQuery
            .Where(je => je.ReferenceType == "PaymentReconciliation"
                      && je.ReferenceId == input.PaymentVoucherId
                      && je.Status == Core.DocumentStatus.Posted)
            .ToList();

        foreach (var je in relatedJes)
        {
            je.Cancel();
            await _journalEntryRepository.UpdateAsync(je);
        }
    }

    /// <summary>
    /// Concurrency-safe AmountPaid update with retry on optimistic concurrency conflict.
    /// </summary>
    private async Task UpdateInvoiceAmountPaidAsync(string invoiceType, Guid invoiceId, decimal amount)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (invoiceType == "SalesInvoice")
                {
                    var si = await _salesInvoiceRepository.GetAsync(invoiceId);
                    si.AmountPaid = Math.Max(0, si.AmountPaid + amount);
                    await _salesInvoiceRepository.UpdateAsync(si, autoSave: true);
                }
                else if (invoiceType == "PurchaseInvoice")
                {
                    var pi = await _purchaseInvoiceRepository.GetAsync(invoiceId);
                    pi.AmountPaid = Math.Max(0, pi.AmountPaid + amount);
                    await _purchaseInvoiceRepository.UpdateAsync(pi, autoSave: true);
                }
                return;
            }
            catch (Volo.Abp.Data.AbpDbConcurrencyException) when (attempt < 3)
            {
                Logger.LogWarning(
                    "Concurrency conflict updating {InvoiceType} {InvoiceId} AmountPaid (attempt {Attempt}/3)",
                    invoiceType, invoiceId, attempt);
                await Task.Delay(attempt * 10);
            }
        }
    }
}
