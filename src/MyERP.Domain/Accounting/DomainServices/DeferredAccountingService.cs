using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Deferred revenue and deferred expense, end to end. Port of ERPNext accounts/deferred_revenue.py
/// plus the deferred branches of the Sales/Purchase Invoice GL composers.
///
/// Invoice time — a deferred row never reaches income/expense:
///   Sales Invoice     DR Receivable            CR Deferred Revenue (row)
///   Purchase Invoice  DR Deferred Expense (row) CR Payable
///
/// Recognition — one entry per row per booking period, moving the period's share across:
///   Revenue  DR Deferred Revenue (row)  CR Income
///   Expense  DR Expense                 CR Deferred Expense (row)
///
/// Every deferred-account line carries the invoice row id (<see cref="JournalEntryLine.ReferenceDetailId"/>),
/// which is how "what reached the deferred account" and "how much is already recognized" are
/// answered per row, the same job ERPNext's voucher_detail_no / reference_detail_no does.
///
/// Credit notes (ERPNext PR #64259 "reverse unrecognized deferred revenue on returns"): a credit note row
/// reverses the deferred account only while recognition of the row it returns has not started;
/// once it has, the row reverses income as before. Recognition of the original then covers only
/// the part of the sale that was kept.
/// </summary>
public class DeferredAccountingService : DomainService
{
    public const string RevenueReferenceType = "DeferredRevenue";
    public const string ExpenseReferenceType = "DeferredExpense";
    private const string SalesInvoiceVoucherType = "SalesInvoice";
    private const string PurchaseInvoiceVoucherType = "PurchaseInvoice";

    /// <summary>Against-voucher type stamped on entries booked by a Process Deferred Accounting run.</summary>
    public const string ProcessVoucherType = "ProcessDeferredAccounting";

    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<AccountsSettings, Guid>? _accountsSettingsRepository;

    public DeferredAccountingService(
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<AccountsSettings, Guid>? accountsSettingsRepository = null)
    {
        _journalEntryRepository = journalEntryRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _companyRepository = companyRepository;
        _accountsSettingsRepository = accountsSettingsRepository;
    }

    // ------------------------------------------------------------------------------------------
    // Invoice time: which rows post to their deferred account
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Rows of a Sales Invoice whose income posts to deferred revenue. Per ERPNext
    /// SalesInvoiceGLComposer.get_item_income_account():
    /// - row not deferred: income;
    /// - invoice (or debit note): deferred revenue;
    /// - credit note without an original, or a row with no original row: income;
    /// - credit note row: deferred revenue only while the original row's recognition has not
    ///   started (see <see cref="ResolveDeferredReturnRowsAsync"/>), otherwise income.
    /// </summary>
    public async Task<List<DeferredCarveOutLine>> ResolveRevenueCarveOutsAsync(SalesInvoice invoice)
    {
        var rows = invoice.Items
            .Where(i => i.EnableDeferredRevenue && i.DeferredRevenueAccountId.HasValue)
            .ToList();
        if (rows.Count == 0) return new List<DeferredCarveOutLine>();

        if (invoice.IsReturn)
        {
            if (!invoice.ReturnAgainstId.HasValue) return new List<DeferredCarveOutLine>();

            var deferredRowIds = await ResolveDeferredReturnRowsAsync(invoice, rows);
            rows = rows.Where(r => deferredRowIds.Contains(r.Id)).ToList();
            if (rows.Count == 0) return new List<DeferredCarveOutLine>();
        }

        var netAmounts = GetRowNetAmounts(invoice.Items.Select(i => (i.Id, i.LineTotal)), invoice.NetTotal);
        return rows
            .Where(r => netAmounts[r.Id] != 0)
            .Select(r => new DeferredCarveOutLine(r.DeferredRevenueAccountId!.Value, netAmounts[r.Id], r.Id))
            .ToList();
    }

    /// <summary>
    /// Rows of a Purchase Invoice whose cost posts to deferred expense. Per ERPNext
    /// PurchaseInvoiceGLComposer: a deferred row debits its deferred expense account, except on a
    /// return (debit note), which always reverses the expense account.
    /// </summary>
    public List<DeferredCarveOutLine> ResolveExpenseCarveOuts(PurchaseInvoice invoice)
    {
        if (invoice.IsReturn) return new List<DeferredCarveOutLine>();

        var rows = invoice.Items
            .Where(i => i.EnableDeferredExpense && i.DeferredExpenseAccountId.HasValue)
            .ToList();
        if (rows.Count == 0) return new List<DeferredCarveOutLine>();

        var netAmounts = GetRowNetAmounts(invoice.Items.Select(i => (i.Id, i.LineTotal)), invoice.NetTotal);
        return rows
            .Where(r => netAmounts[r.Id] != 0)
            .Select(r => new DeferredCarveOutLine(r.DeferredExpenseAccountId!.Value, netAmounts[r.Id], r.Id))
            .ToList();
    }

    /// <summary>
    /// Credit note rows that reverse deferred revenue. Port of ERPNext
    /// SalesInvoiceGLComposer.deferred_return_items.
    /// </summary>
    /// <remarks>
    /// A row that already posted to its deferred account keeps doing so: a GL repost composes the
    /// entries again and must not flip the row to income just because recognition of the original
    /// has started since. Otherwise the row qualifies when the original row is deferred into the
    /// same account, actually credited it, and nothing has been recognized against it yet — draft
    /// recognition journals count as started.
    ///
    /// ERPNext links a credit note row to its original through sales_invoice_item. Rows here carry
    /// no such link, so the original row is found the way the rest of the return logic finds it:
    /// same item, and here also the same deferred account.
    /// </remarks>
    private async Task<HashSet<Guid>> ResolveDeferredReturnRowsAsync(SalesInvoice creditNote, List<SalesInvoiceItem> rows)
    {
        var ownJournals = await GetJournalsAsync(SalesInvoiceVoucherType, new[] { creditNote.Id });
        var deferredRowIds = rows
            .Where(r => ownJournals
                .Where(j => j.VoucherType != JournalEntryVoucherType.Reversal)
                .Any(j => j.Lines.Any(l => l.ReferenceDetailId == r.Id && l.AccountId == r.DeferredRevenueAccountId)))
            .Select(r => r.Id)
            .ToHashSet();

        var undecided = rows.Where(r => !deferredRowIds.Contains(r.Id)).ToList();
        if (undecided.Count == 0) return deferredRowIds;

        var original = await _salesInvoiceRepository.FindAsync(creditNote.ReturnAgainstId!.Value);
        if (original == null) return deferredRowIds;

        var originalJournals = await GetJournalsAsync(SalesInvoiceVoucherType, new[] { original.Id });
        var recognitionJournals = await GetJournalsAsync(RevenueReferenceType, new[] { original.Id });

        foreach (var row in undecided)
        {
            var originalRow = FindOriginalRow(original, row.ItemId, row.DeferredRevenueAccountId!.Value);
            if (originalRow == null) continue;

            // The original row must actually sit in the deferred account, or there is nothing here
            // for the credit note to reverse.
            if (GetNetPosted(originalJournals, originalRow.Id, row.DeferredRevenueAccountId.Value, creditSide: true).Base == 0)
                continue;

            // Keep the existing treatment once recognition has started, including draft journals.
            var booked = GetNetPosted(recognitionJournals, originalRow.Id, row.DeferredRevenueAccountId.Value, creditSide: false);
            if (booked.Base != 0 || booked.Transaction != 0) continue;

            deferredRowIds.Add(row.Id);
        }

        return deferredRowIds;
    }

    /// <summary>
    /// Blocks cancelling a credit note that reversed deferred revenue once the original invoice has
    /// recognition booked. Port of ERPNext validate_deferred_revenue_return_cancellation(): that
    /// recognition was sized for the sale net of this credit note, so the credit note cannot
    /// vanish from under it — the recognition entries have to be cancelled first.
    /// </summary>
    public async Task ValidateReturnCancellationAsync(SalesInvoice creditNote)
    {
        if (!creditNote.IsReturn || !creditNote.ReturnAgainstId.HasValue) return;

        var rows = creditNote.Items
            .Where(i => i.EnableDeferredRevenue && i.DeferredRevenueAccountId.HasValue)
            .ToList();
        if (rows.Count == 0) return;

        var ownJournals = await GetJournalsAsync(SalesInvoiceVoucherType, new[] { creditNote.Id });
        var deferredRows = rows
            .Where(r => GetNetPosted(ownJournals, r.Id, r.DeferredRevenueAccountId!.Value, creditSide: true).Base != 0)
            .ToList();
        if (deferredRows.Count == 0) return;

        var original = await _salesInvoiceRepository.FindAsync(creditNote.ReturnAgainstId.Value);
        if (original == null) return;

        var recognitionJournals = await GetJournalsAsync(RevenueReferenceType, new[] { original.Id });
        foreach (var row in deferredRows)
        {
            var originalRow = FindOriginalRow(original, row.ItemId, row.DeferredRevenueAccountId!.Value);
            if (originalRow == null) continue;

            var booked = GetNetPosted(recognitionJournals, originalRow.Id, row.DeferredRevenueAccountId.Value, creditSide: false);
            if (booked.Base != 0 || booked.Transaction != 0)
            {
                throw new BusinessException(MyERPDomainErrorCodes.DeferredRevenueBookedOnReturnedInvoice)
                    .WithData("invoiceNumber", original.InvoiceNumber);
            }
        }
    }

    // ------------------------------------------------------------------------------------------
    // Recognition
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Books deferred revenue for every eligible Sales Invoice up to <paramref name="asOfDate"/>.
    /// Port of ERPNext convert_deferred_revenue_to_income(). Returns the number of entries booked.
    /// </summary>
    /// <param name="startDate">
    /// Lower edge of the service window an invoice must overlap. Null catches up everything still
    /// unbooked, which is what the scheduled job wants; Process Deferred Accounting passes its own
    /// start date like ERPNext does.
    /// </param>
    /// <param name="deferredProcessId">
    /// The Process Deferred Accounting document doing the booking, if any. Its entries are stamped
    /// with it so cancelling that document can find and reverse exactly what it booked.
    /// </param>
    public async Task<int> ProcessDeferredRevenueAsync(
        Guid companyId, DateTime asOfDate, Guid? tenantId = null, Guid? accountId = null, DateTime? startDate = null,
        Guid? deferredProcessId = null)
    {
        var plan = await PlanDeferredRevenueAsync(companyId, asOfDate, accountId, startDate);
        return await BookAsync(plan, companyId, tenantId, isRevenue: true, deferredProcessId);
    }

    /// <summary>
    /// Books deferred expense for every eligible Purchase Invoice up to <paramref name="asOfDate"/>.
    /// Port of ERPNext convert_deferred_expense_to_expense().
    /// </summary>
    public async Task<int> ProcessDeferredExpenseAsync(
        Guid companyId, DateTime asOfDate, Guid? tenantId = null, Guid? accountId = null, DateTime? startDate = null,
        Guid? deferredProcessId = null)
    {
        var plan = await PlanDeferredExpenseAsync(companyId, asOfDate, accountId, startDate);
        return await BookAsync(plan, companyId, tenantId, isRevenue: false, deferredProcessId);
    }

    /// <summary>What <see cref="ProcessDeferredRevenueAsync"/> would book, without booking it.</summary>
    public async Task<List<DeferredBookingEntry>> PlanDeferredRevenueAsync(
        Guid companyId, DateTime asOfDate, Guid? accountId = null, DateTime? startDate = null)
    {
        asOfDate = asOfDate.Date;
        var query = await _salesInvoiceRepository.GetQueryableAsync();
        var invoices = query
            .Where(si => si.CompanyId == companyId && si.Status == DocumentStatus.Posted && !si.IsReturn)
            .ToList()
            .Where(si => si.Items.Any(i =>
                i.EnableDeferredRevenue
                && i.DeferredRevenueAccountId.HasValue
                && IsInServiceWindow(i.ServiceStartDate, i.ServiceEndDate, i.LineTotal, startDate, asOfDate)
                && (!accountId.HasValue || i.DeferredRevenueAccountId == accountId.Value)))
            .ToList();
        if (invoices.Count == 0) return new List<DeferredBookingEntry>();

        var invoiceIds = invoices.Select(i => i.Id).ToList();
        var invoiceJournals = (await GetJournalsAsync(SalesInvoiceVoucherType, invoiceIds)).ToLookup(j => j.ReferenceId!.Value);
        var recognitionJournals = (await GetJournalsAsync(RevenueReferenceType, invoiceIds)).ToLookup(j => j.ReferenceId!.Value);
        var returnedByInvoice = await GetDeferredRevenueReturnsAsync(invoices);
        var basedOn = await GetBookingBasisAsync();
        var frozenTill = (await _companyRepository.GetAsync(companyId)).AccountsFrozenTillDate;

        var plan = new List<DeferredBookingEntry>();
        foreach (var invoice in invoices)
        {
            var netAmounts = GetRowNetAmounts(invoice.Items.Select(i => (i.Id, i.LineTotal)), invoice.NetTotal);
            returnedByInvoice.TryGetValue(invoice.Id, out var returned);

            foreach (var item in invoice.Items.Where(i =>
                         i.EnableDeferredRevenue && i.DeferredRevenueAccountId.HasValue
                         && i.ServiceStartDate.HasValue && i.ServiceEndDate.HasValue))
            {
                var deferredAccountId = item.DeferredRevenueAccountId!.Value;

                // An invoice posted before deferred rows were routed to their deferred account
                // credited income in full. Recognizing it would count that income a second time,
                // so it waits for a GL repost.
                if (GetNetPosted(invoiceJournals[invoice.Id], item.Id, deferredAccountId, creditSide: true).Base == 0)
                    continue;

                // Recognize only the retained sale without changing the posted invoice.
                var net = netAmounts[item.Id];
                if (returned != null && returned.TryGetValue(item.Id, out var returnedNet))
                    net = Math.Max(0m, net - returnedNet);

                plan.AddRange(PlanRow(new DeferredRowContext
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    RowId = item.Id,
                    ItemId = item.ItemId,
                    Description = item.Description,
                    DeferredAccountId = deferredAccountId,
                    ServiceStartDate = item.ServiceStartDate!.Value.Date,
                    ServiceEndDate = item.ServiceEndDate!.Value.Date,
                    ServiceStopDate = item.ServiceStopDate?.Date,
                    NetAmount = net,
                    BaseNetAmount = ToBase(net, invoice.ExchangeRate),
                    CurrencyCode = invoice.CurrencyCode,
                    ExchangeRate = invoice.ExchangeRate,
                    CostCenterId = invoice.CostCenterId,
                    ProjectId = invoice.ProjectId,
                }, recognitionJournals[invoice.Id], asOfDate, basedOn, frozenTill, bookedOnDebitSide: true));
            }
        }

        return plan;
    }

    /// <summary>What <see cref="ProcessDeferredExpenseAsync"/> would book, without booking it.</summary>
    public async Task<List<DeferredBookingEntry>> PlanDeferredExpenseAsync(
        Guid companyId, DateTime asOfDate, Guid? accountId = null, DateTime? startDate = null)
    {
        asOfDate = asOfDate.Date;
        var query = await _purchaseInvoiceRepository.GetQueryableAsync();
        var invoices = query
            .Where(pi => pi.CompanyId == companyId && pi.Status == DocumentStatus.Posted && !pi.IsReturn)
            .ToList()
            .Where(pi => pi.Items.Any(i =>
                i.EnableDeferredExpense
                && i.DeferredExpenseAccountId.HasValue
                && IsInServiceWindow(i.ServiceStartDate, i.ServiceEndDate, i.LineTotal, startDate, asOfDate)
                && (!accountId.HasValue || i.DeferredExpenseAccountId == accountId.Value)))
            .ToList();
        if (invoices.Count == 0) return new List<DeferredBookingEntry>();

        var invoiceIds = invoices.Select(i => i.Id).ToList();
        var invoiceJournals = (await GetJournalsAsync(PurchaseInvoiceVoucherType, invoiceIds)).ToLookup(j => j.ReferenceId!.Value);
        var recognitionJournals = (await GetJournalsAsync(ExpenseReferenceType, invoiceIds)).ToLookup(j => j.ReferenceId!.Value);
        var basedOn = await GetBookingBasisAsync();
        var frozenTill = (await _companyRepository.GetAsync(companyId)).AccountsFrozenTillDate;

        var plan = new List<DeferredBookingEntry>();
        foreach (var invoice in invoices)
        {
            var netAmounts = GetRowNetAmounts(invoice.Items.Select(i => (i.Id, i.LineTotal)), invoice.NetTotal);

            foreach (var item in invoice.Items.Where(i =>
                         i.EnableDeferredExpense && i.DeferredExpenseAccountId.HasValue
                         && i.ServiceStartDate.HasValue && i.ServiceEndDate.HasValue))
            {
                var deferredAccountId = item.DeferredExpenseAccountId!.Value;

                // Same guard as revenue: only cost that was actually parked in the deferred account.
                if (GetNetPosted(invoiceJournals[invoice.Id], item.Id, deferredAccountId, creditSide: false).Base == 0)
                    continue;

                var net = netAmounts[item.Id];
                plan.AddRange(PlanRow(new DeferredRowContext
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    RowId = item.Id,
                    ItemId = item.ItemId,
                    Description = item.Description,
                    DeferredAccountId = deferredAccountId,
                    ServiceStartDate = item.ServiceStartDate!.Value.Date,
                    ServiceEndDate = item.ServiceEndDate!.Value.Date,
                    ServiceStopDate = item.ServiceStopDate?.Date,
                    NetAmount = net,
                    BaseNetAmount = ToBase(net, invoice.ExchangeRate),
                    CurrencyCode = invoice.CurrencyCode,
                    ExchangeRate = invoice.ExchangeRate,
                    CostCenterId = invoice.CostCenterId,
                    ProjectId = invoice.ProjectId,
                }, recognitionJournals[invoice.Id], asOfDate, basedOn, frozenTill, bookedOnDebitSide: false));
            }
        }

        return plan;
    }

    /// <summary>
    /// Reverses every recognition entry booked for an invoice. ERPNext books recognition as GL
    /// entries of the invoice itself, so cancelling the invoice takes them with it; here they are
    /// separate journals and have to be reversed alongside the invoice's own GL.
    /// </summary>
    public async Task ReverseRecognitionAsync(string referenceType, Guid invoiceId)
    {
        var journals = await GetJournalsAsync(referenceType, new[] { invoiceId });
        await ReverseAsync(journals, journals);
    }

    /// <summary>
    /// Reverses the entries one Process Deferred Accounting run booked. Port of ERPNext
    /// ProcessDeferredAccounting.on_cancel(), which reverses the GL entries whose against voucher
    /// is that document. Returns the number of entries reversed.
    /// </summary>
    public async Task<int> ReverseProcessAsync(Guid deferredProcessId, bool isRevenue)
    {
        var referenceType = isRevenue ? RevenueReferenceType : ExpenseReferenceType;
        var query = await _journalEntryRepository.GetQueryableAsync();
        var booked = query
            .Where(j => j.ReferenceType == referenceType
                        && j.VoucherType != JournalEntryVoucherType.Reversal
                        && (j.Status == DocumentStatus.Posted || j.Status == DocumentStatus.Draft)
                        && j.Lines.Any(l => l.AgainstVoucherType == ProcessVoucherType && l.AgainstVoucherId == deferredProcessId))
            .ToList();
        if (booked.Count == 0) return 0;

        // An invoice cancelled since the run already took its entries back.
        var bookedIds = booked.Select(j => j.Id).ToList();
        var reversals = query
            .Where(j => j.VoucherType == JournalEntryVoucherType.Reversal
                        && j.ReversalOfId.HasValue
                        && bookedIds.Contains(j.ReversalOfId.Value)
                        && j.Status == DocumentStatus.Posted)
            .ToList();

        return await ReverseAsync(booked, reversals);
    }

    /// <summary>
    /// Contra-reverses the given recognition journals, skipping reversal journals themselves and
    /// anything already reversed (looked up in <paramref name="scope"/>). A draft never reached
    /// the ledger, so it is simply dropped.
    /// </summary>
    private async Task<int> ReverseAsync(IEnumerable<JournalEntry> journals, IEnumerable<JournalEntry> scope)
    {
        var reversedIds = scope
            .Where(j => j.VoucherType == JournalEntryVoucherType.Reversal && j.ReversalOfId.HasValue)
            .Select(j => j.ReversalOfId!.Value)
            .ToHashSet();

        var count = 0;
        foreach (var journal in journals.Where(j => j.VoucherType != JournalEntryVoucherType.Reversal && !reversedIds.Contains(j.Id)).ToList())
        {
            count++;
            if (journal.Status == DocumentStatus.Draft)
            {
                await _journalEntryRepository.DeleteAsync(journal);
                continue;
            }

            var reversal = new JournalEntry(
                GuidGenerator.Create(), journal.CompanyId, journal.FiscalYearId, journal.PostingDate, journal.TenantId)
            {
                ReferenceType = journal.ReferenceType,
                ReferenceId = journal.ReferenceId,
                ReferenceNumber = journal.ReferenceNumber,
                VoucherType = JournalEntryVoucherType.Reversal,
                ReversalOfId = journal.Id,
                IsMultiCurrency = journal.IsMultiCurrency,
            };
            foreach (var line in journal.Lines)
                reversal.AddReversalLine(line);

            reversal.Post();
            await _journalEntryRepository.InsertAsync(reversal);
        }

        return count;
    }

    private async Task<int> BookAsync(
        List<DeferredBookingEntry> plan, Guid companyId, Guid? tenantId, bool isRevenue, Guid? deferredProcessId = null)
    {
        if (plan.Count == 0) return 0;

        var company = await _companyRepository.GetAsync(companyId);
        var counterAccountId = (isRevenue ? company.DefaultIncomeAccountId : company.DefaultExpenseAccountId)
            ?? throw new BusinessException(MyERPDomainErrorCodes.DefaultAccountNotConfigured)
                .WithData("reason", isRevenue
                    ? "No income account configured. Set Default Income Account in Company settings."
                    : "No expense account configured. Set Default Expense Account in Company settings.");

        // Per ERPNext: entries are left as drafts only when they are booked via Journal Entry and
        // Accounts Settings says not to submit them. A draft still counts as booked from then on.
        var settings = await GetSettingsAsync();
        var leaveAsDraft = settings is { BookDeferredEntriesViaJournalEntry: true, SubmitJournalEntries: false };

        var fiscalYears = (await _fiscalYearRepository.GetQueryableAsync())
            .Where(f => f.CompanyId == companyId)
            .ToList();

        foreach (var entry in plan)
        {
            var fiscalYear = fiscalYears.FirstOrDefault(f => f.StartDate <= entry.PostingDate && f.EndDate >= entry.PostingDate)
                ?? throw new BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                    .WithData("reason", $"No fiscal year covers posting date {entry.PostingDate:yyyy-MM-dd}. Create a fiscal year for this period.");

            var journal = new JournalEntry(GuidGenerator.Create(), companyId, fiscalYear.Id, entry.PostingDate, tenantId)
            {
                ReferenceType = isRevenue ? RevenueReferenceType : ExpenseReferenceType,
                ReferenceId = entry.InvoiceId,
                ReferenceNumber = entry.InvoiceNumber,
                VoucherType = isRevenue ? JournalEntryVoucherType.DeferredRevenue : JournalEntryVoucherType.DeferredExpense,
                IsMultiCurrency = entry.ExchangeRate != 1m,
            };

            var description = $"{(isRevenue ? "Deferred Revenue" : "Deferred Expense")}: {entry.InvoiceNumber} - {entry.Description}";

            // Revenue: DR Deferred Revenue / CR Income. Expense: DR Expense / CR Deferred Expense.
            // A negative amount (more already booked than the row now carries, e.g. after a credit
            // note) books the same pair the other way round.
            var deferredIsDebit = isRevenue ? entry.BaseAmount > 0 : entry.BaseAmount < 0;
            journal.AddLine(BuildLine(journal.Id, entry.DeferredAccountId, entry, deferredIsDebit, description, deferredProcessId));
            journal.AddLine(BuildLine(journal.Id, counterAccountId, entry, !deferredIsDebit, description, deferredProcessId));

            journal.Validate();
            if (!leaveAsDraft) journal.Post();
            await _journalEntryRepository.InsertAsync(journal);
        }

        return plan.Count;
    }

    private static JournalEntryLine BuildLine(
        Guid journalId, Guid accountId, DeferredBookingEntry entry, bool isDebit, string description, Guid? deferredProcessId)
    {
        var line = new JournalEntryLine(Guid.NewGuid(), journalId, accountId, Math.Abs(entry.BaseAmount), isDebit, description)
        {
            CostCenterId = entry.CostCenterId,
            ProjectId = entry.ProjectId,
            ReferenceDetailId = entry.RowId,
            AgainstVoucherType = deferredProcessId.HasValue ? ProcessVoucherType : null,
            AgainstVoucherId = deferredProcessId,
        };

        if (entry.ExchangeRate != 1m)
        {
            line.AccountCurrency = entry.CurrencyCode;
            line.AmountInAccountCurrency = Math.Abs(entry.Amount);
            line.ExchangeRate = entry.ExchangeRate;
        }

        return line;
    }

    /// <summary>
    /// Walks one row forward period by period until it is booked up to <paramref name="postingDate"/>.
    /// Port of the _book_deferred_revenue_or_expense() recursion in ERPNext.
    /// </summary>
    private static List<DeferredBookingEntry> PlanRow(
        DeferredRowContext row,
        IEnumerable<JournalEntry> recognitionJournals,
        DateTime postingDate,
        string basedOn,
        DateTime? accountsFrozenTill,
        bool bookedOnDebitSide)
    {
        var journals = recognitionJournals.ToList();
        var booked = GetNetPosted(journals, row.RowId, row.DeferredAccountId, creditSide: !bookedOnDebitSide);
        var previousPostingDate = GetLastBookedDate(journals, row.RowId, row.DeferredAccountId);

        var entries = new List<DeferredBookingEntry>();
        foreach (var period in WalkPeriods(
                     row.ServiceStartDate, row.ServiceEndDate, row.ServiceStopDate, row.NetAmount, row.BaseNetAmount,
                     basedOn, postingDate, previousPostingDate, booked.Transaction, booked.Base))
        {
            // Books that are frozen up to the period end take the entry on the last day of the
            // month the freeze ends in.
            var glPostingDate = period.End;
            if (accountsFrozenTill.HasValue && period.End <= accountsFrozenTill.Value.Date)
                glPostingDate = LastDayOfMonth(accountsFrozenTill.Value.Date.AddDays(1));

            entries.Add(new DeferredBookingEntry
            {
                InvoiceId = row.InvoiceId,
                InvoiceNumber = row.InvoiceNumber,
                RowId = row.RowId,
                ItemId = row.ItemId,
                Description = row.Description,
                DeferredAccountId = row.DeferredAccountId,
                ServiceStartDate = row.ServiceStartDate,
                ServiceEndDate = row.ServiceEndDate,
                PeriodStart = period.Start,
                PeriodEnd = period.End,
                PostingDate = glPostingDate,
                TotalAmount = row.BaseNetAmount,
                Amount = period.Amount,
                BaseAmount = period.BaseAmount,
                CurrencyCode = row.CurrencyCode,
                ExchangeRate = row.ExchangeRate,
                CostCenterId = row.CostCenterId,
                ProjectId = row.ProjectId,
                IsLast = period.IsLast,
            });
        }

        return entries;
    }

    /// <summary>
    /// The whole recognition schedule of a row as it would be booked from scratch up to
    /// <paramref name="postingDate"/> — nothing booked yet, no freeze.
    /// </summary>
    public static List<DeferredBookingPeriod> ProjectSchedule(
        DateTime serviceStartDate,
        DateTime serviceEndDate,
        DateTime? serviceStopDate,
        decimal netAmount,
        decimal baseNetAmount,
        string basedOn,
        DateTime postingDate)
        => WalkPeriods(
                serviceStartDate.Date, serviceEndDate.Date, serviceStopDate?.Date, netAmount, baseNetAmount,
                basedOn, postingDate.Date, previousPostingDate: null, alreadyBooked: 0m, alreadyBookedBase: 0m)
            .ToList();

    /// <summary>
    /// Booking periods still open for a row, each with its amount, given what is already booked.
    /// A period whose amount comes out at zero is skipped but still moves the schedule on.
    /// </summary>
    private static IEnumerable<DeferredBookingPeriod> WalkPeriods(
        DateTime serviceStartDate,
        DateTime serviceEndDate,
        DateTime? serviceStopDate,
        decimal netAmount,
        decimal baseNetAmount,
        string basedOn,
        DateTime postingDate,
        DateTime? previousPostingDate,
        decimal alreadyBooked,
        decimal alreadyBookedBase)
    {
        while (true)
        {
            var period = GetBookingDates(serviceStartDate, serviceEndDate, serviceStopDate, postingDate, previousPostingDate);
            if (period == null) yield break;

            var (amount, baseAmount) = CalculateAmount(
                basedOn, netAmount, baseNetAmount, alreadyBooked, alreadyBookedBase,
                serviceStartDate, serviceEndDate, period.Value.Start, period.Value.End, period.Value.IsLast);

            if (amount != 0 && baseAmount != 0)
            {
                yield return new DeferredBookingPeriod(period.Value.Start, period.Value.End, period.Value.IsLast, amount, baseAmount);
                alreadyBooked += amount;
                alreadyBookedBase += baseAmount;
            }

            previousPostingDate = period.Value.End;
            if (period.Value.End >= postingDate || period.Value.IsLast) yield break;
        }
    }

    // ------------------------------------------------------------------------------------------
    // Pure booking arithmetic (ERPNext get_booking_dates / calculate_amount / calculate_monthly_amount)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Next period to book for a row: from the day after the last booking (or the service start)
    /// to the end of that month, cut short by the service end / stop date — which makes it the
    /// last entry — and by the posting date. Null when the row is already booked that far.
    /// </summary>
    internal static (DateTime Start, DateTime End, bool IsLast)? GetBookingDates(
        DateTime serviceStartDate,
        DateTime serviceEndDate,
        DateTime? serviceStopDate,
        DateTime postingDate,
        DateTime? previousPostingDate)
    {
        var start = previousPostingDate.HasValue ? previousPostingDate.Value.Date.AddDays(1) : serviceStartDate.Date;
        var end = LastDayOfMonth(start);
        var isLast = false;

        if (end >= serviceEndDate.Date)
        {
            end = serviceEndDate.Date;
            isLast = true;
        }
        else if (serviceStopDate.HasValue && end >= serviceStopDate.Value.Date)
        {
            end = serviceStopDate.Value.Date;
            isLast = true;
        }

        if (end > postingDate.Date) end = postingDate.Date;

        return start <= end ? (start, end, isLast) : null;
    }

    /// <summary>
    /// Amount to book for one period, in transaction and company currency. The last entry always
    /// takes whatever is left, so the deferred account closes exactly.
    /// "Days": the row's amount in proportion to the days booked.
    /// "Months": an equal share per month of service, prorated for a partial first or last month.
    /// </summary>
    internal static (decimal Amount, decimal BaseAmount) CalculateAmount(
        string basedOn,
        decimal netAmount,
        decimal baseNetAmount,
        decimal alreadyBooked,
        decimal alreadyBookedBase,
        DateTime serviceStartDate,
        DateTime serviceEndDate,
        DateTime periodStart,
        DateTime periodEnd,
        bool isLast)
    {
        if (isLast)
            return (Math.Round(netAmount - alreadyBooked, 2), Math.Round(baseNetAmount - alreadyBookedBase, 2));

        if (!string.Equals(basedOn, "Months", StringComparison.OrdinalIgnoreCase))
        {
            decimal totalDays = (serviceEndDate.Date - serviceStartDate.Date).Days + 1;
            decimal bookingDays = (periodEnd.Date - periodStart.Date).Days + 1;
            return (Math.Round(netAmount * bookingDays / totalDays, 2), Math.Round(baseNetAmount * bookingDays / totalDays, 2));
        }

        var totalMonths = ((serviceEndDate.Year - serviceStartDate.Year) * 12) + (serviceEndDate.Month - serviceStartDate.Month) + 1;
        var prorateFactor = (decimal)(serviceEndDate.Date - serviceStartDate.Date).Days
            / (LastDayOfMonth(serviceEndDate) - FirstDayOfMonth(serviceStartDate)).Days;
        var actualMonths = Math.Round(totalMonths * prorateFactor, 1);
        if (actualMonths == 0) return (0m, 0m);

        var baseAmount = Math.Round(baseNetAmount / actualMonths, 2);
        if (baseAmount + alreadyBookedBase > baseNetAmount) baseAmount = baseNetAmount - alreadyBookedBase;

        var amount = Math.Round(netAmount / actualMonths, 2);
        if (amount + alreadyBooked > netAmount) amount = netAmount - alreadyBooked;

        if (FirstDayOfMonth(periodStart) != periodStart.Date || LastDayOfMonth(periodEnd) != periodEnd.Date)
        {
            var partialMonth = Math.Round(
                (decimal)(periodEnd.Date - periodStart.Date).Days / (LastDayOfMonth(periodEnd) - FirstDayOfMonth(periodStart)).Days, 1);
            baseAmount = partialMonth * baseAmount;
            amount = partialMonth * amount;
        }

        return (amount, baseAmount);
    }

    /// <summary>
    /// Each row's net amount: its line total, scaled when the invoice's net total differs from the
    /// sum of the lines (an invoice-level discount), which is how the discount is spread over rows.
    /// </summary>
    internal static Dictionary<Guid, decimal> GetRowNetAmounts(IEnumerable<(Guid Id, decimal LineTotal)> rows, decimal netTotal)
    {
        var list = rows.ToList();
        var lineSum = list.Sum(r => r.LineTotal);
        var factor = lineSum != 0 && netTotal != lineSum ? netTotal / lineSum : 1m;
        return list.ToDictionary(r => r.Id, r => Math.Round(r.LineTotal * factor, 2));
    }

    private static bool IsInServiceWindow(
        DateTime? serviceStartDate, DateTime? serviceEndDate, decimal amount, DateTime? windowStart, DateTime windowEnd)
    {
        if (!serviceStartDate.HasValue || !serviceEndDate.HasValue || amount <= 0) return false;
        if (serviceStartDate.Value.Date > windowEnd) return false;
        return !windowStart.HasValue || serviceEndDate.Value.Date >= windowStart.Value.Date;
    }

    /// <summary>Company-currency value of a row, rounded the way the invoice's own GL line is.</summary>
    private static decimal ToBase(decimal amount, decimal exchangeRate)
        => exchangeRate == 1m ? amount : Math.Round(amount * exchangeRate, 4);

    private static DateTime FirstDayOfMonth(DateTime date) => new(date.Year, date.Month, 1);

    private static DateTime LastDayOfMonth(DateTime date)
        => new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    // ------------------------------------------------------------------------------------------
    // Ledger lookups
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Net amount a row has on an account across a set of journals — reversal journals contra
    /// their originals line by line (and keep the row id), so the plain sum is what is still in
    /// effect. Draft journals are included: ERPNext counts unsubmitted recognition journals as
    /// booked, and no other journal type read here is ever left in draft.
    /// </summary>
    private static (decimal Base, decimal Transaction) GetNetPosted(
        IEnumerable<JournalEntry> journals, Guid rowId, Guid accountId, bool creditSide)
    {
        decimal baseAmount = 0, transactionAmount = 0;
        foreach (var line in journals.SelectMany(j => j.Lines)
                     .Where(l => l.ReferenceDetailId == rowId && l.AccountId == accountId))
        {
            var sign = line.IsDebit == creditSide ? -1m : 1m;
            baseAmount += sign * line.Amount;
            transactionAmount += sign * line.AmountInAccountCurrency;
        }

        return (baseAmount, transactionAmount);
    }

    /// <summary>Posting date of the latest recognition entry still in effect for a row.</summary>
    private static DateTime? GetLastBookedDate(List<JournalEntry> journals, Guid rowId, Guid accountId)
    {
        var reversedIds = journals
            .Where(j => j.VoucherType == JournalEntryVoucherType.Reversal && j.ReversalOfId.HasValue)
            .Select(j => j.ReversalOfId!.Value)
            .ToHashSet();

        var dates = journals
            .Where(j => j.VoucherType != JournalEntryVoucherType.Reversal
                        && !reversedIds.Contains(j.Id)
                        && j.Lines.Any(l => l.ReferenceDetailId == rowId && l.AccountId == accountId))
            .Select(j => j.PostingDate.Date)
            .ToList();

        return dates.Count > 0 ? dates.Max() : null;
    }

    /// <summary>
    /// Net amounts of posted credit note rows that reversed deferred revenue, per original invoice
    /// and original row. Port of ERPNext get_deferred_revenue_returns().
    /// </summary>
    private async Task<Dictionary<Guid, Dictionary<Guid, decimal>>> GetDeferredRevenueReturnsAsync(List<SalesInvoice> invoices)
    {
        var result = new Dictionary<Guid, Dictionary<Guid, decimal>>();
        var invoiceIds = invoices.Select(i => i.Id).ToList();

        var query = await _salesInvoiceRepository.GetQueryableAsync();
        var creditNotes = query
            .Where(si => si.IsReturn
                         && si.Status == DocumentStatus.Posted
                         && si.ReturnAgainstId.HasValue
                         && invoiceIds.Contains(si.ReturnAgainstId.Value))
            .ToList();
        if (creditNotes.Count == 0) return result;

        var creditNoteJournals = (await GetJournalsAsync(SalesInvoiceVoucherType, creditNotes.Select(c => c.Id).ToList()))
            .ToLookup(j => j.ReferenceId!.Value);

        foreach (var invoice in invoices)
        {
            // Returned net per (item, deferred account), then handed to the original rows in order.
            var returnedByKey = new Dictionary<(Guid ItemId, Guid AccountId), decimal>();
            foreach (var creditNote in creditNotes.Where(c => c.ReturnAgainstId == invoice.Id && c.CompanyId == invoice.CompanyId))
            {
                var netAmounts = GetRowNetAmounts(creditNote.Items.Select(i => (i.Id, i.LineTotal)), creditNote.NetTotal);
                foreach (var row in creditNote.Items.Where(i => i.EnableDeferredRevenue && i.DeferredRevenueAccountId.HasValue))
                {
                    var accountId = row.DeferredRevenueAccountId!.Value;
                    if (GetNetPosted(creditNoteJournals[creditNote.Id], row.Id, accountId, creditSide: true).Base == 0)
                        continue;

                    var key = (row.ItemId, accountId);
                    returnedByKey.TryGetValue(key, out var soFar);
                    returnedByKey[key] = soFar - netAmounts[row.Id];
                }
            }
            if (returnedByKey.Count == 0) continue;

            var originalNet = GetRowNetAmounts(invoice.Items.Select(i => (i.Id, i.LineTotal)), invoice.NetTotal);
            var byRow = new Dictionary<Guid, decimal>();
            foreach (var group in invoice.Items
                         .Where(i => i.EnableDeferredRevenue && i.DeferredRevenueAccountId.HasValue)
                         .GroupBy(i => (i.ItemId, AccountId: i.DeferredRevenueAccountId!.Value)))
            {
                if (!returnedByKey.TryGetValue(group.Key, out var remaining) || remaining <= 0) continue;

                var rows = group.OrderBy(i => i.Idx).ToList();
                for (var i = 0; i < rows.Count && remaining > 0; i++)
                {
                    // The last row takes whatever is left so nothing returned goes unaccounted.
                    var share = i == rows.Count - 1 ? remaining : Math.Min(remaining, Math.Max(0m, originalNet[rows[i].Id]));
                    if (share <= 0) continue;
                    byRow[rows[i].Id] = share;
                    remaining -= share;
                }
            }

            if (byRow.Count > 0) result[invoice.Id] = byRow;
        }

        return result;
    }

    private static SalesInvoiceItem? FindOriginalRow(SalesInvoice original, Guid itemId, Guid deferredAccountId)
        => original.Items
            .Where(i => i.ItemId == itemId && i.EnableDeferredRevenue && i.DeferredRevenueAccountId == deferredAccountId)
            .OrderBy(i => i.Idx)
            .FirstOrDefault();

    private async Task<List<JournalEntry>> GetJournalsAsync(string referenceType, IReadOnlyCollection<Guid> referenceIds)
    {
        if (referenceIds.Count == 0) return new List<JournalEntry>();

        var ids = referenceIds.ToList();
        var query = await _journalEntryRepository.GetQueryableAsync();
        return query
            .Where(j => j.ReferenceType == referenceType
                        && j.ReferenceId.HasValue
                        && ids.Contains(j.ReferenceId.Value)
                        && (j.Status == DocumentStatus.Posted || j.Status == DocumentStatus.Draft))
            .ToList();
    }

    private async Task<AccountsSettings?> GetSettingsAsync()
    {
        var repository = _accountsSettingsRepository
            ?? LazyServiceProvider?.LazyGetService<IRepository<AccountsSettings, Guid>>();
        if (repository == null) return null;
        return (await repository.GetQueryableAsync()).FirstOrDefault();
    }

    private async Task<string> GetBookingBasisAsync()
        => (await GetSettingsAsync())?.BookDeferredEntriesBasedOn ?? "Days";

    private sealed class DeferredRowContext
    {
        public Guid InvoiceId { get; init; }
        public string InvoiceNumber { get; init; } = null!;
        public Guid RowId { get; init; }
        public Guid ItemId { get; init; }
        public string Description { get; init; } = null!;
        public Guid DeferredAccountId { get; init; }
        public DateTime ServiceStartDate { get; init; }
        public DateTime ServiceEndDate { get; init; }
        public DateTime? ServiceStopDate { get; init; }
        public decimal NetAmount { get; init; }
        public decimal BaseNetAmount { get; init; }
        public string CurrencyCode { get; init; } = null!;
        public decimal ExchangeRate { get; init; }
        public Guid? CostCenterId { get; init; }
        public Guid? ProjectId { get; init; }
    }
}

/// <summary>One booking period of a deferred row and the amount it carries.</summary>
public readonly record struct DeferredBookingPeriod(DateTime Start, DateTime End, bool IsLast, decimal Amount, decimal BaseAmount);

/// <summary>One recognition entry: a row's share for one booking period.</summary>
public class DeferredBookingEntry
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;

    /// <summary>Invoice row the entry belongs to.</summary>
    public Guid RowId { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public Guid DeferredAccountId { get; set; }
    public DateTime ServiceStartDate { get; set; }
    public DateTime ServiceEndDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    /// <summary>Ledger date — the period end, unless the books are frozen up to it.</summary>
    public DateTime PostingDate { get; set; }

    /// <summary>The row's whole deferred amount in company currency, net of credit notes.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Amount for this period in transaction currency.</summary>
    public decimal Amount { get; set; }

    /// <summary>Amount for this period in company currency.</summary>
    public decimal BaseAmount { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; } = 1m;
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }

    /// <summary>True for the entry that closes the row out.</summary>
    public bool IsLast { get; set; }
}
