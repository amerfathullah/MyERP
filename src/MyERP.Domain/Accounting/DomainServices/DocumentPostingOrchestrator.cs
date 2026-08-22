using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
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
    private readonly AccountingDimensionService _dimensionService;
    private readonly CompanySettingsCache _companySettingsCache;
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;
    private readonly IRepository<AccountingPeriod, Guid> _periodRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly WarehouseAccountService _warehouseAccountService;

    public DocumentPostingOrchestrator(
        AccountingRuleEngine ruleEngine,
        PaymentLedgerService pleService,
        BudgetValidationService budgetValidationService,
        AccountingDimensionService dimensionService,
        CompanySettingsCache companySettingsCache,
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<PaymentLedgerEntry, Guid> pleRepository,
        IRepository<AccountingPeriod, Guid> periodRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        WarehouseAccountService warehouseAccountService)
    {
        _ruleEngine = ruleEngine;
        _pleService = pleService;
        _budgetValidationService = budgetValidationService;
        _dimensionService = dimensionService;
        _companySettingsCache = companySettingsCache;
        _journalRepository = journalRepository;
        _pleRepository = pleRepository;
        _periodRepository = periodRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _sleRepository = sleRepository;
        _warehouseAccountService = warehouseAccountService;
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

        // Step 2: Validate mandatory accounting dimensions on GL lines
        await _dimensionService.ValidateMandatoryDimensionsAsync(invoice.CompanyId, journal.Lines);

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

        // Validate mandatory accounting dimensions on GL lines
        await _dimensionService.ValidateMandatoryDimensionsAsync(invoice.CompanyId, journal.Lines);

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
    /// <remarks>
    /// Builds the main JE directly from paidFromAccountId/paidToAccountId — deliberately does
    /// NOT go through AccountingRuleEngine.PostDocumentAsync (unlike SI/PI/DN/PR, which always
    /// post the same two accounts and fit that generic AccountSource/AmountSource config model).
    /// A Payment Entry's two GL legs are picked PER-TRANSACTION by the user (which bank account,
    /// which party account) and flip direction between Receive and Pay — the seeded "PaymentEntry"
    /// AccountingRule rows (DR FixedAccount / CR CustomerReceivable, both unconditional) can only
    /// ever express one direction. Confirmed empirically (EfCorePaymentEntryGlDirectionTests) that
    /// routing a Pay-type Payment Entry through the generic engine posted DR a hardcoded bank
    /// account / CR the company's default Receivable — never touching Payable or the payment's
    /// own PaidFrom/PaidTo accounts at all. The correct, direction-agnostic rule is simply
    /// DR PaidToAccountId / CR PaidFromAccountId: for Receive (PaidFrom=Receivable, PaidTo=Bank)
    /// that's DR Bank / CR Receivable; for Pay (PaidFrom=Bank, PaidTo=Payable) that's DR Payable /
    /// CR Bank — matching the seeded rule's own "for received payments" comment, which was only
    /// ever correct for the one direction it was written for.
    /// </remarks>
    public async Task<JournalEntry> PostPaymentEntryAsync(
        IAccountableDocument payment,
        Guid partyAccountId,
        string partyType,
        Guid partyId,
        string accountCurrency,
        decimal exchangeRate,
        PaymentAllocation[] allocations,
        Guid paidFromAccountId,
        Guid paidToAccountId)
    {
        await ValidatePostingPeriodAsync(payment.CompanyId, payment.PostingDate, payment.DocumentType);

        var fiscalYear = await _fiscalYearRepository.FindAsync(fy =>
            fy.CompanyId == payment.CompanyId &&
            !fy.IsClosed &&
            fy.StartDate <= payment.PostingDate &&
            fy.EndDate >= payment.PostingDate);
        if (fiscalYear == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("date", payment.PostingDate);
        }

        var journal = new JournalEntry(GuidGenerator.Create(), payment.CompanyId, fiscalYear.Id, payment.PostingDate)
        {
            ReferenceType = payment.DocumentType,
            ReferenceId = payment.Id,
        };

        var isMultiCurrency = exchangeRate != 1m;
        var amountInTransactionCurrency = payment.GrandTotal;
        var amountInCompanyCurrency = isMultiCurrency
            ? Math.Round(amountInTransactionCurrency * exchangeRate, 4)
            : amountInTransactionCurrency;

        journal.AddLineWithDimensions(paidToAccountId, amountInCompanyCurrency, true,
            payment.CostCenterId, null, payment.FinanceBook);
        if (isMultiCurrency)
        {
            journal.Lines[^1].AccountCurrency = accountCurrency;
            journal.Lines[^1].AmountInAccountCurrency = amountInTransactionCurrency;
            journal.Lines[^1].ExchangeRate = exchangeRate;
        }

        journal.AddLineWithDimensions(paidFromAccountId, amountInCompanyCurrency, false,
            payment.CostCenterId, null, payment.FinanceBook);
        if (isMultiCurrency)
        {
            journal.Lines[^1].AccountCurrency = accountCurrency;
            journal.Lines[^1].AmountInAccountCurrency = amountInTransactionCurrency;
            journal.Lines[^1].ExchangeRate = exchangeRate;
        }

        journal.Validate();
        journal.Post();

        // Validate mandatory accounting dimensions on GL lines
        await _dimensionService.ValidateMandatoryDimensionsAsync(payment.CompanyId, journal.Lines);

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
        await _dimensionService.ValidateMandatoryDimensionsAsync(deliveryNote.CompanyId, journal.Lines);
        await _journalRepository.InsertAsync(journal);
        return journal;
    }

    /// <summary>
    /// Post a Delivery Note with warehouse-specific stock account (per-warehouse GL).
    /// DR: COGS account, CR: warehouse-specific stock account (resolved by WarehouseAccountService).
    /// Per ERPNext BaseStockGLComposer: uses warehouse account for CR Stock, company default for DR COGS.
    /// </summary>
    public async Task<JournalEntry> PostDeliveryNoteAsync(
        IAccountableDocument deliveryNote,
        Guid stockAccountId,
        Guid? sdbnbAccountId = null)
    {
        await ValidatePostingPeriodAsync(deliveryNote.CompanyId, deliveryNote.PostingDate, deliveryNote.DocumentType);

        var journal = await _ruleEngine.PostDocumentAsync(deliveryNote, stockAccountId);
        await _dimensionService.ValidateMandatoryDimensionsAsync(deliveryNote.CompanyId, journal.Lines);
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
        await _dimensionService.ValidateMandatoryDimensionsAsync(purchaseReceipt.CompanyId, journal.Lines);
        await _journalRepository.InsertAsync(journal);
        return journal;
    }

    /// <summary>
    /// Post a Purchase Receipt with warehouse-specific GL accounts.
    /// DR: warehouse-specific stock account, CR: SRBNB account (resolved by WarehouseAccountService).
    /// Per ERPNext BaseStockGLComposer: uses WarehouseAccount for DR Stock, company for CR SRBNB.
    /// </summary>
    public async Task<JournalEntry> PostPurchaseReceiptAsync(
        IAccountableDocument purchaseReceipt,
        Guid stockAccountId,
        Guid? srbnbAccountId = null)
    {
        await ValidatePostingPeriodAsync(purchaseReceipt.CompanyId, purchaseReceipt.PostingDate, purchaseReceipt.DocumentType);

        var journal = await _ruleEngine.PostDocumentAsync(purchaseReceipt, stockAccountId);
        await _dimensionService.ValidateMandatoryDimensionsAsync(purchaseReceipt.CompanyId, journal.Lines);
        await _journalRepository.InsertAsync(journal);
        return journal;
    }

    /// <summary>
    /// Post a Stock Entry (perpetual inventory): creates GL entries for stock movement, built
    /// directly from the StockLedgerEntry rows StockPostingService already created for this entry
    /// (so GL always matches what actually moved — no separate rate recomputation).
    /// </summary>
    /// <remarks>
    /// Deliberately bypasses AccountingRuleEngine (unlike SI/PI/DN/PR, which always post the same
    /// two accounts and fit that generic config model) — confirmed via DefaultDataSeeder and
    /// CompanyAppService.SeedCompanyDefaultsAsync that ZERO "StockEntry" AccountingRule rows are
    /// ever seeded anywhere in this codebase, and there is no AppService or Angular screen to add
    /// them, so the generic path was completely unreachable for every Stock Entry purpose,
    /// forever, in every company (76th migration session finding). Stock Entry GL genuinely needs
    /// per-purpose treatment the generic engine can't express (it fires every matching rule
    /// unconditionally, with no purpose/condition field) — same shape of mismatch the 75th
    /// session's Payment Entry fix addressed, just with more distinct shapes here.
    ///
    /// Handles Material Receipt/Issue/Transfer, Manufacture, Disassemble, Repack, and
    /// SendToSubcontractor (see the case group's remarks below). Every remaining StockEntryType
    /// (Material Consumption For Manufacture, Subcontracting Delivery/Return, Adjustment) throws a
    /// clear, explicit error rather than guessing at GL treatment — same "throws" outcome they
    /// always had (zero rules = always threw), just with an honest reason now instead of a generic
    /// "no rules configured".
    /// </remarks>
    public async Task<JournalEntry> PostStockEntryAsync(StockEntry stockEntry)
    {
        await ValidatePostingPeriodAsync(stockEntry.CompanyId, stockEntry.PostingDate, ((IAccountableDocument)stockEntry).DocumentType);

        var fiscalYear = await _fiscalYearRepository.FindAsync(fy =>
            fy.CompanyId == stockEntry.CompanyId &&
            !fy.IsClosed &&
            fy.StartDate <= stockEntry.PostingDate &&
            fy.EndDate >= stockEntry.PostingDate);
        if (fiscalYear == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("date", stockEntry.PostingDate);
        }

        var company = await _companyRepository.GetAsync(stockEntry.CompanyId);

        var sles = await _sleRepository.GetListAsync(s =>
            s.VoucherType == "StockEntry" && s.VoucherId == stockEntry.Id);

        var journal = new JournalEntry(GuidGenerator.Create(), stockEntry.CompanyId, fiscalYear.Id, stockEntry.PostingDate)
        {
            ReferenceType = ((IAccountableDocument)stockEntry).DocumentType,
            ReferenceId = stockEntry.Id,
        };

        if (sles.Count > 0)
        {
            switch (stockEntry.EntryType)
            {
                case StockEntryType.MaterialIssue:
                    await BuildIssueOrReceiptLinesAsync(journal, company, sles, isIssue: true);
                    break;

                case StockEntryType.MaterialReceipt:
                case StockEntryType.ReceiveAtWarehouse:
                    await BuildIssueOrReceiptLinesAsync(journal, company, sles, isIssue: false);
                    break;

                case StockEntryType.MaterialConsumptionForManufacture:
                    // Structurally identical to Material Issue (source-only SLEs, single "other
                    // side" account) but the RM hasn't left the company for good — it's been
                    // reclassified into work-in-process ahead of the Manufacture entry that
                    // completes the FG, so the offsetting debit is DefaultWipAccountId (an ASSET),
                    // never DefaultExpenseAccountId. See ManufacturingAppService.RecordProductionAsync
                    // and CreateManufactureStockEntryAsync's GetPriorMaterialConsumptionValueAsync
                    // calls for the other half of this: folding this value back into the FG's cost
                    // once it's produced. NOTE: the later Manufacture entry's own GL still credits
                    // this pre-consumed value to Stock Adjustment (via BuildStockToStockLinesAsync's
                    // generic residual plug), not back to WIP — so WIP accumulates rather than
                    // clearing to zero on FG completion. Deliberately left unresolved this session
                    // (would require Manufacture's builder to know about linked prior consumption
                    // entries); flagged for a dedicated follow-up.
                    await BuildMaterialConsumptionLinesAsync(journal, company, sles);
                    break;

                case StockEntryType.MaterialTransfer:
                case StockEntryType.MaterialTransferForManufacture:
                case StockEntryType.SendToWarehouse:
                case StockEntryType.SendToSubcontractor:
                case StockEntryType.Manufacture:
                case StockEntryType.Disassemble:
                case StockEntryType.Repack:
                    // Per StockEntryManager.ValidateWarehousesAsync's own "isTransfer" bucket:
                    // MaterialTransferForManufacture, SendToWarehouse, and (as of this session)
                    // SendToSubcontractor all require both a source and target warehouse on every
                    // item exactly like MaterialTransfer — structurally and economically the same
                    // "no P&L impact" stock-to-stock movement, just to a WIP, transit, or supplier
                    // warehouse instead of an arbitrary one. SendToSubcontractor's target
                    // (SubcontractingOrder.SupplierWarehouseId) is a plain Warehouse row like any
                    // other — WarehouseAccountService's 5-level resolution chain (direct mapping ->
                    // warehouse's own DefaultAccountId -> parent chain -> company default) doesn't
                    // care that it happens to represent supplier-held stock, so no bespoke
                    // "supplier-owned-inventory" account plumbing was needed, contrary to what the
                    // 76th session assumed without reading CreateRmTransferStockEntryAsync.
                    //
                    // Repack: per StockPostingService, each item row posts an SLE off its OWN
                    // SourceWarehouseId/TargetWarehouseId independently (same mechanism Manufacture
                    // and Disassemble already use), so it produces the identical
                    // negative-SLE-for-RM-out / positive-SLE-for-FG-in shape — no separate handling
                    // needed. Single-FG Repack balances exactly by construction
                    // (StockEntryManager.CalculateRepackFgRate prices the FG at
                    // total_outgoing_cost/fg_qty); multi-FG Repack requires each FG's rate to be set
                    // manually (ValidateRepackItems), so it can leave a genuine residual — same as
                    // Disassemble, already plugged to Stock Adjustment below.
                    await BuildStockToStockLinesAsync(journal, company, sles);
                    break;

                default:
                    throw new BusinessException(MyERPDomainErrorCodes.StockEntryGlPurposeNotSupported)
                        .WithData("documentType", ((IAccountableDocument)stockEntry).DocumentType)
                        .WithData("purpose", stockEntry.EntryType.ToString());
            }
        }

        await _dimensionService.ValidateMandatoryDimensionsAsync(stockEntry.CompanyId, journal.Lines);

        if (journal.Lines.Count > 0)
        {
            journal.Validate();
            journal.Post();
            await _journalRepository.InsertAsync(journal);
        }

        return journal;
    }

    /// <summary>Material Issue: DR Expense (total), CR Stock(warehouse) per SLE. Material
    /// Receipt/ReceiveAtWarehouse: DR Stock(warehouse) per SLE, CR Stock Adjustment (total).</summary>
    private async Task BuildIssueOrReceiptLinesAsync(
        JournalEntry journal, Company company, List<StockLedgerEntry> sles, bool isIssue)
    {
        var otherAccountId = isIssue ? company.DefaultExpenseAccountId : company.DefaultStockAdjustmentAccountId;
        var reason = isIssue
            ? "No expense account configured. Set Default Expense Account in Company settings."
            : "No stock adjustment account configured. Set Default Stock Adjustment Account in Company settings.";
        await BuildSingleSidedStockLinesAsync(journal, company, sles, isStockOut: isIssue, otherAccountId, reason);
    }

    /// <summary>Material Consumption For Manufacture: DR WIP (total), CR Stock(warehouse) per SLE —
    /// same source-only stock-out mechanics as Material Issue, but the RM hasn't left the company
    /// for good. It's been reclassified into work-in-process ahead of the Manufacture entry that
    /// completes the FG, so the offsetting debit is an ASSET account, never Expense.</summary>
    private async Task BuildMaterialConsumptionLinesAsync(
        JournalEntry journal, Company company, List<StockLedgerEntry> sles)
    {
        await BuildSingleSidedStockLinesAsync(journal, company, sles, isStockOut: true, company.DefaultWipAccountId,
            "No WIP account configured. Set Default WIP Account in Company settings.");
    }

    /// <summary>Shared "one other account, N per-warehouse stock lines" shape used by both the
    /// Issue/Receipt builder and the Material Consumption builder above.</summary>
    private async Task BuildSingleSidedStockLinesAsync(
        JournalEntry journal, Company company, List<StockLedgerEntry> sles,
        bool isStockOut, Guid? otherAccountId, string missingAccountReason)
    {
        var total = sles.Sum(s => Math.Abs(s.StockValue));
        if (total <= 0) return;

        if (!otherAccountId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DefaultAccountNotConfigured)
                .WithData("reason", missingAccountReason);
        }
        journal.AddLine(otherAccountId.Value, total, isDebit: isStockOut);

        foreach (var sle in sles)
        {
            var stockAccountId = await _warehouseAccountService.ResolveStockAccountAsync(sle.WarehouseId, company.Id);
            journal.AddLine(stockAccountId, Math.Abs(sle.StockValue), isDebit: !isStockOut);
        }
    }

    /// <summary>Transfer/Manufacture/Disassemble: per SLE, DR the resolved stock account for
    /// stock-in (positive StockValue), CR for stock-out (negative). Manufacture and Transfer
    /// balance exactly by construction (the caller derives FG/target value from the same RM/source
    /// cost); Disassemble's FG-out (priced at current balance) and RM-in (priced at a stored or
    /// current rate) can genuinely differ, so any residual is plugged to the Stock Adjustment
    /// account rather than left to fail JournalEntry.Validate()'s zero-tolerance balance check.</summary>
    private async Task BuildStockToStockLinesAsync(JournalEntry journal, Company company, List<StockLedgerEntry> sles)
    {
        decimal debitTotal = 0, creditTotal = 0;
        foreach (var sle in sles)
        {
            if (sle.StockValue == 0) continue;
            var stockAccountId = await _warehouseAccountService.ResolveStockAccountAsync(sle.WarehouseId, company.Id);
            var isDebit = sle.StockValue > 0;
            journal.AddLine(stockAccountId, Math.Abs(sle.StockValue), isDebit: isDebit);
            if (isDebit) debitTotal += sle.StockValue; else creditTotal += -sle.StockValue;
        }

        var difference = debitTotal - creditTotal;
        if (difference == 0) return;

        if (!company.DefaultStockAdjustmentAccountId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DefaultAccountNotConfigured)
                .WithData("reason", "No stock adjustment account configured. Set Default Stock Adjustment Account in Company settings.");
        }
        // debit-heavy (difference > 0) needs a credit to balance, and vice versa.
        journal.AddLine(company.DefaultStockAdjustmentAccountId.Value, Math.Abs(difference), isDebit: difference < 0);
    }

    /// <summary>
    /// Reverses the posted GL Journal Entry linked to a cancelled document, if one exists, by
    /// posting a new contra entry — same posting date, every line's debit/credit flipped, full
    /// party/dimension fidelity preserved (see JournalEntry.AddReversalLine). Per the
    /// accounts-controller cancel protocol: the original entry is never touched — both it and the
    /// reversal stay Status=Posted, netting to zero within the period while remaining individually
    /// visible for audit. No-op if the document never posted GL (no matching entry found). Relies on
    /// the caller's own document-status guard (e.g. entity.Cancel() throwing on an already-cancelled
    /// document) to prevent this being invoked twice for the same voucher — it does not itself
    /// detect an existing reversal, matching ReversePleForDocumentAsync's existing behavior below.
    /// </summary>
    public async Task ReverseGlForDocumentAsync(string voucherType, Guid voucherId)
    {
        var query = await _journalRepository.GetQueryableAsync();
        // Excludes ExchangeGainOrLoss and PaymentTax: a Payment Entry can have its main GL JE plus
        // one or more per-reference exchange gain/loss JEs AND a separate tax JE, all sharing the
        // same (ReferenceType, ReferenceId) — those are reversed separately via
        // ReverseExchangeGainLossJournalEntriesAsync / ReversePaymentTaxJournalEntriesAsync, since an
        // unreconcile/cancel of one shouldn't touch the others.
        var original = query.FirstOrDefault(j =>
            j.ReferenceType == voucherType && j.ReferenceId == voucherId
            && j.Status == DocumentStatus.Posted
            && j.VoucherType != JournalEntryVoucherType.Reversal
            && j.VoucherType != JournalEntryVoucherType.ExchangeGainOrLoss
            && j.VoucherType != JournalEntryVoucherType.PaymentTax);

        if (original == null) return;

        await ReverseJournalEntryAsync(original);
    }

    /// <summary>
    /// Reverses every posted exchange gain/loss Journal Entry linked to a document — e.g. the
    /// per-reference FX JEs a multi-currency Payment Entry posts alongside its main GL. Reverses
    /// ALL matches (not just the first), unlike <see cref="ReverseGlForDocumentAsync"/>, since a
    /// single Payment Entry can post one FX JE per allocated reference. No-op if none are posted.
    /// </summary>
    public async Task ReverseExchangeGainLossJournalEntriesAsync(string voucherType, Guid voucherId)
    {
        var query = await _journalRepository.GetQueryableAsync();
        var entries = query.Where(j =>
            j.ReferenceType == voucherType && j.ReferenceId == voucherId
            && j.Status == DocumentStatus.Posted
            && j.VoucherType == JournalEntryVoucherType.ExchangeGainOrLoss).ToList();

        foreach (var entry in entries)
            await ReverseJournalEntryAsync(entry);
    }

    /// <summary>
    /// Reverses every posted Payment Entry tax Journal Entry linked to a document — see
    /// PaymentEntryAppService.PostAsync's "Payment Entry Tax GL Posting" block, which builds this
    /// as a separate JE from the payment's main GL. Only one is ever posted per Payment Entry today,
    /// but reverses ALL matches for the same reason <see cref="ReverseExchangeGainLossJournalEntriesAsync"/>
    /// does — no-op if none are posted.
    /// </summary>
    public async Task ReversePaymentTaxJournalEntriesAsync(string voucherType, Guid voucherId)
    {
        var query = await _journalRepository.GetQueryableAsync();
        var entries = query.Where(j =>
            j.ReferenceType == voucherType && j.ReferenceId == voucherId
            && j.Status == DocumentStatus.Posted
            && j.VoucherType == JournalEntryVoucherType.PaymentTax).ToList();

        foreach (var entry in entries)
            await ReverseJournalEntryAsync(entry);
    }

    /// <summary>
    /// Same contra-entry reversal as <see cref="ReverseGlForDocumentAsync"/>, but looked up by
    /// the Journal Entry's own Id rather than a (ReferenceType, ReferenceId) pair — for callers
    /// like Asset depreciation JEs that never set those reference fields (each period's JE is
    /// linked back via DepreciationScheduleEntry.JournalEntryId instead), so there's nothing
    /// for the ReferenceType-based lookup to find. No-op if the JE isn't Posted (already
    /// reversed, or never existed).
    /// </summary>
    public async Task ReverseGlForJournalEntryAsync(Guid journalEntryId)
    {
        var original = await _journalRepository.FindAsync(journalEntryId);
        if (original == null || original.Status != DocumentStatus.Posted
            || original.VoucherType == JournalEntryVoucherType.Reversal)
        {
            return;
        }

        await ReverseJournalEntryAsync(original);
    }

    private async Task ReverseJournalEntryAsync(JournalEntry original)
    {
        var reversal = new JournalEntry(
            GuidGenerator.Create(), original.CompanyId, original.FiscalYearId, original.PostingDate, original.TenantId)
        {
            ReferenceType = original.ReferenceType,
            ReferenceId = original.ReferenceId,
            ReferenceNumber = original.ReferenceNumber,
            VoucherType = JournalEntryVoucherType.Reversal,
            ReversalOfId = original.Id,
            IsMultiCurrency = original.IsMultiCurrency,
        };

        foreach (var line in original.Lines)
            reversal.AddReversalLine(line);

        reversal.Post();
        await _journalRepository.InsertAsync(reversal);
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
    /// Per ERPNext: accounting period closure is per-document-type, not blanket.
    /// Users with the period's exempted role can bypass.
    /// Public so cancel paths can also validate before reversing.
    /// </summary>
    public async Task ValidatePostingPeriodAsync(Guid companyId, DateTime postingDate, string documentType, IEnumerable<string>? currentUserRoles = null)
    {
        // Check accounts frozen date — uses cached company settings (5-min TTL)
        // to avoid hitting the database on every single posting operation
        var cachedSettings = await _companySettingsCache.GetAsync(companyId);
        if (cachedSettings.AccountsFrozenTillDate.HasValue && postingDate <= cachedSettings.AccountsFrozenTillDate.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AccountingPeriodClosed)
                .WithData("frozenTill", cachedSettings.AccountsFrozenTillDate.Value.ToString("yyyy-MM-dd"))
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

        // Check accounting period closure — per document type
        var periodsQuery = await _periodRepository.GetQueryableAsync();
        var closedPeriod = periodsQuery
            .Where(p => p.IsClosed
                && p.StartDate <= postingDate
                && p.EndDate >= postingDate
                && p.CompanyId == companyId)
            .FirstOrDefault();

        if (closedPeriod != null && closedPeriod.IsClosedForDocumentType(documentType))
        {
            // Exempted role bypass: if user has the exempted role, allow through
            if (!string.IsNullOrWhiteSpace(closedPeriod.ExemptedRole)
                && currentUserRoles != null
                && currentUserRoles.Contains(closedPeriod.ExemptedRole, StringComparer.OrdinalIgnoreCase))
            {
                return; // bypass — user has the exempted role
            }

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
