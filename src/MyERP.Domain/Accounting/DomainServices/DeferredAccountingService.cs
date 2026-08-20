using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Handles deferred revenue AND expense recognition by generating periodic Journal Entries.
/// Per ERPNext: both are recognized over the service period (monthly proration).
/// Catch-up logic: if missed periods exist, generates ALL missed JEs.
/// Final period uses (total - already_booked) for exact match.
/// 
/// GL Pattern for Revenue (per period):
///   DR Deferred Revenue (reduce liability)   → deferredRevenueAccountId
///   CR Revenue (recognize income)            → company.DefaultIncomeAccountId
///
/// GL Pattern for Expense (per period):
///   DR Expense (recognize expense)           → company.DefaultExpenseAccountId
///   CR Deferred Expense (reduce prepayment)  → deferredExpenseAccountId
/// </summary>
public class DeferredAccountingService : DomainService
{
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public DeferredAccountingService(
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _journalEntryRepository = journalEntryRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Generates deferred revenue recognition JEs for all eligible invoices up to the given date.
    /// Called by background scheduler (monthly) or manual trigger.
    /// </summary>
    public async Task<int> ProcessDeferredRevenueAsync(Guid companyId, DateTime asOfDate, Guid? tenantId = null)
    {
        var invoiceQuery = await _salesInvoiceRepository.GetQueryableAsync();
        var eligibleInvoices = invoiceQuery
            .Where(si => si.CompanyId == companyId
                      && si.Status == Core.DocumentStatus.Posted)
            .ToList();

        // Find existing deferred revenue JEs to avoid double-booking
        var jeQuery = await _journalEntryRepository.GetQueryableAsync();
        var existingDeferredJes = jeQuery
            .Where(je => je.CompanyId == companyId
                      && je.ReferenceType == "DeferredRevenue"
                      && je.Status == Core.DocumentStatus.Posted)
            .Select(je => je.PostingDate)
            .ToHashSet();

        int jeCount = 0;

        foreach (var invoice in eligibleInvoices)
        {
            var deferredItems = invoice.Items
                .Where(i => i.EnableDeferredRevenue
                         && i.ServiceStartDate.HasValue
                         && i.ServiceEndDate.HasValue
                         && i.DeferredRevenueAccountId.HasValue)
                .ToList();

            foreach (var item in deferredItems)
            {
                var schedule = GenerateSchedule(item, asOfDate);
                foreach (var entry in schedule)
                {
                    // Skip periods already booked (check existing JE dates) or future periods
                    if (existingDeferredJes.Contains(entry.PostingDate)) continue;
                    if (entry.PostingDate > asOfDate) continue;

                    var je = await CreateRecognitionJEAsync(
                        invoice.CompanyId,
                        entry.Amount,
                        item.DeferredRevenueAccountId!.Value,
                        entry.PostingDate,
                        $"Deferred Revenue: {invoice.InvoiceNumber} - {item.Description}",
                        tenantId);

                    await _journalEntryRepository.InsertAsync(je);
                    existingDeferredJes.Add(entry.PostingDate); // Track so we don't double-book
                    jeCount++;
                }
            }
        }

        return jeCount;
    }

    /// <summary>
    /// Generates deferred expense recognition JEs for all eligible purchase invoices up to the given date.
    /// Mirrors deferred revenue logic but with reversed GL direction.
    /// GL: DR Expense (recognize cost), CR Deferred Expense (reduce prepayment).
    /// </summary>
    public async Task<int> ProcessDeferredExpenseAsync(Guid companyId, DateTime asOfDate, Guid? tenantId = null)
    {
        var invoiceQuery = await _purchaseInvoiceRepository.GetQueryableAsync();
        var eligibleInvoices = invoiceQuery
            .Where(pi => pi.CompanyId == companyId
                      && pi.Status == Core.DocumentStatus.Posted)
            .ToList();

        var jeQuery = await _journalEntryRepository.GetQueryableAsync();
        var existingDeferredJes = jeQuery
            .Where(je => je.CompanyId == companyId
                      && je.ReferenceType == "DeferredExpense"
                      && je.Status == Core.DocumentStatus.Posted)
            .Select(je => je.PostingDate)
            .ToHashSet();

        int jeCount = 0;

        foreach (var invoice in eligibleInvoices)
        {
            var deferredItems = invoice.Items
                .Where(i => i.EnableDeferredExpense
                         && i.ServiceStartDate.HasValue
                         && i.ServiceEndDate.HasValue
                         && i.DeferredExpenseAccountId.HasValue)
                .ToList();

            foreach (var item in deferredItems)
            {
                var schedule = GenerateExpenseSchedule(item, asOfDate);
                foreach (var entry in schedule)
                {
                    if (existingDeferredJes.Contains(entry.PostingDate)) continue;
                    if (entry.PostingDate > asOfDate) continue;

                    var je = await CreateExpenseRecognitionJEAsync(
                        invoice.CompanyId,
                        entry.Amount,
                        item.DeferredExpenseAccountId!.Value,
                        entry.PostingDate,
                        $"Deferred Expense: {invoice.InvoiceNumber} - {item.Description}",
                        tenantId);

                    await _journalEntryRepository.InsertAsync(je);
                    existingDeferredJes.Add(entry.PostingDate);
                    jeCount++;
                }
            }
        }

        return jeCount;
    }

    /// <summary>
    /// Generates the monthly schedule for a deferred expense item.
    /// Same proration logic as deferred revenue.
    /// </summary>
    public List<DeferredScheduleEntry> GenerateExpenseSchedule(PurchaseInvoiceItem item, DateTime asOfDate)
    {
        if (!item.ServiceStartDate.HasValue || !item.ServiceEndDate.HasValue)
            return new List<DeferredScheduleEntry>();

        var start = item.ServiceStartDate.Value;
        var end = item.ServiceStopDate.HasValue && item.ServiceStopDate.Value < item.ServiceEndDate.Value
            ? item.ServiceStopDate.Value
            : item.ServiceEndDate.Value;
        var totalAmount = item.LineTotal;

        var totalMonths = ((end.Year - start.Year) * 12) + end.Month - start.Month + 1;
        if (totalMonths <= 0) totalMonths = 1;

        var monthlyAmount = Math.Round(totalAmount / totalMonths, 2);
        var entries = new List<DeferredScheduleEntry>();
        decimal bookedSoFar = 0;

        for (int i = 0; i < totalMonths; i++)
        {
            var periodDate = start.AddMonths(i);
            var postingDate = new DateTime(periodDate.Year, periodDate.Month,
                DateTime.DaysInMonth(periodDate.Year, periodDate.Month));

            var amount = (i == totalMonths - 1) ? totalAmount - bookedSoFar : monthlyAmount;

            entries.Add(new DeferredScheduleEntry
            {
                PostingDate = postingDate,
                Amount = amount,
                AlreadyBooked = postingDate < asOfDate.AddMonths(-1),
                PeriodIndex = i + 1,
                TotalPeriods = totalMonths,
            });

            bookedSoFar += amount;
        }

        return entries;
    }

    private async Task<JournalEntry> CreateExpenseRecognitionJEAsync(
        Guid companyId, decimal amount, Guid deferredExpenseAccountId,
        DateTime postingDate, string description, Guid? tenantId)
    {
        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var fy = fyQuery.FirstOrDefault(f =>
            f.CompanyId == companyId && f.StartDate <= postingDate && f.EndDate >= postingDate);
        if (fy == null)
        {
            throw new Volo.Abp.BusinessException("MyERP:02002")
                .WithData("reason", $"No fiscal year covers posting date {postingDate:yyyy-MM-dd}.");
        }

        var company = await _companyRepository.GetAsync(companyId);
        var expenseAccountId = company.DefaultExpenseAccountId ?? deferredExpenseAccountId;

        var je = new JournalEntry(GuidGenerator.Create(), companyId, fy.Id, postingDate, tenantId);
        je.ReferenceType = "DeferredExpense";

        // DR: Expense (recognize cost for this period)
        je.AddLine(expenseAccountId, amount, true, description);
        // CR: Deferred Expense (reduce pre-paid expense liability)
        je.AddLine(deferredExpenseAccountId, amount, false, description);

        je.Validate();
        je.Post();
        return je;
    }

    /// <summary>
    /// Generates the monthly schedule for a deferred item.
    /// Uses monthly proration. Final period = total - already_booked (exact match).
    /// </summary>
    public List<DeferredScheduleEntry> GenerateSchedule(SalesInvoiceItem item, DateTime asOfDate)
    {
        if (!item.ServiceStartDate.HasValue || !item.ServiceEndDate.HasValue)
            return new List<DeferredScheduleEntry>();

        var start = item.ServiceStartDate.Value;
        var end = item.ServiceStopDate.HasValue && item.ServiceStopDate.Value < item.ServiceEndDate.Value
            ? item.ServiceStopDate.Value
            : item.ServiceEndDate.Value;
        var totalAmount = item.LineTotal;

        // Calculate total months in service period
        var totalMonths = ((end.Year - start.Year) * 12) + end.Month - start.Month + 1;
        if (totalMonths <= 0) totalMonths = 1;

        var monthlyAmount = Math.Round(totalAmount / totalMonths, 2);
        var entries = new List<DeferredScheduleEntry>();
        decimal bookedSoFar = 0;

        for (int i = 0; i < totalMonths; i++)
        {
            var periodDate = start.AddMonths(i);
            var postingDate = new DateTime(periodDate.Year, periodDate.Month,
                DateTime.DaysInMonth(periodDate.Year, periodDate.Month)); // Last day of month

            // Final period: use remainder for exact match
            var amount = (i == totalMonths - 1) ? totalAmount - bookedSoFar : monthlyAmount;

            entries.Add(new DeferredScheduleEntry
            {
                PostingDate = postingDate,
                Amount = amount,
                AlreadyBooked = postingDate < asOfDate.AddMonths(-1), // Simple heuristic
                PeriodIndex = i + 1,
                TotalPeriods = totalMonths,
            });

            bookedSoFar += amount;
        }

        return entries;
    }

    private async Task<JournalEntry> CreateRecognitionJEAsync(
        Guid companyId, decimal amount, Guid deferredAccountId,
        DateTime postingDate, string description, Guid? tenantId)
    {
        // Resolve fiscal year for the posting date
        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var fy = fyQuery.FirstOrDefault(f =>
            f.CompanyId == companyId && f.StartDate <= postingDate && f.EndDate >= postingDate);
        if (fy == null)
        {
            throw new Volo.Abp.BusinessException("MyERP:02002")
                .WithData("reason", $"No fiscal year covers posting date {postingDate:yyyy-MM-dd}. Create a fiscal year for this period.");
        }
        var fiscalYearId = fy.Id;

        // Resolve income account from company defaults
        var company = await _companyRepository.GetAsync(companyId);
        var incomeAccountId = company.DefaultIncomeAccountId ?? deferredAccountId;

        var je = new JournalEntry(GuidGenerator.Create(), companyId, fiscalYearId, postingDate, tenantId);
        je.ReferenceType = "DeferredRevenue";

        // DR: Deferred Revenue (reduce liability — asset being consumed)
        je.AddLine(deferredAccountId, amount, true, description);
        // CR: Revenue/Income (recognize income for this period)
        je.AddLine(incomeAccountId, amount, false, description);

        je.Validate();
        je.Post();
        return je;
    }
}

/// <summary>Single period in the deferred recognition schedule.</summary>
public class DeferredScheduleEntry
{
    public DateTime PostingDate { get; set; }
    public decimal Amount { get; set; }
    public bool AlreadyBooked { get; set; }
    public int PeriodIndex { get; set; }
    public int TotalPeriods { get; set; }
}
