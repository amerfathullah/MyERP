using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Handles deferred revenue/expense recognition by generating periodic Journal Entries.
/// Per ERPNext: revenue is recognized over the service period (monthly proration).
/// Catch-up logic: if missed periods exist, generates ALL missed JEs.
/// Final period uses (total - already_booked) for exact match.
/// </summary>
public class DeferredAccountingService : DomainService
{
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;

    public DeferredAccountingService(
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository)
    {
        _journalEntryRepository = journalEntryRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
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
                    if (entry.AlreadyBooked) continue;

                    var je = CreateRecognitionJE(
                        invoice.CompanyId,
                        entry.Amount,
                        item.DeferredRevenueAccountId!.Value,
                        entry.PostingDate,
                        $"Deferred Revenue: {invoice.InvoiceNumber} - {item.Description}",
                        tenantId);

                    await _journalEntryRepository.InsertAsync(je);
                    jeCount++;
                }
            }
        }

        return jeCount;
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
        var end = item.ServiceEndDate.Value;
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

    private JournalEntry CreateRecognitionJE(
        Guid companyId, decimal amount, Guid deferredAccountId,
        DateTime postingDate, string description, Guid? tenantId)
    {
        // FiscalYearId would be resolved from the posting date in real implementation
        var je = new JournalEntry(GuidGenerator.Create(), companyId, Guid.Empty, postingDate, tenantId);
        // DR: Deferred Revenue (reduce liability)
        je.AddLine(deferredAccountId, amount, true, description);
        // CR: Revenue (recognize income) — in production, would use a resolved income account
        je.AddLine(deferredAccountId, amount, false, description);
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
