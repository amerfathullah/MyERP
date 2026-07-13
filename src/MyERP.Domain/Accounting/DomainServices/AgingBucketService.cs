using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Calculates AR/AP aging buckets for outstanding invoices.
/// Per ERPNext: aging is based on posting_date or due_date, with configurable bucket ranges.
/// Standard buckets: 0-30, 31-60, 61-90, 91-120, 120+ days.
/// </summary>
public class AgingBucketService : DomainService
{
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;

    public AgingBucketService(
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
    }

    /// <summary>
    /// Calculates AR aging (receivables) for a company as of a given date.
    /// Groups outstanding sales invoices into aging buckets.
    /// </summary>
    public async Task<AgingReport> CalculateReceivablesAgingAsync(
        Guid companyId, DateTime asOfDate, int[] bucketDays = null!)
    {
        bucketDays ??= new[] { 30, 60, 90, 120 };

        var query = await _salesInvoiceRepository.GetQueryableAsync();
        var outstandingInvoices = query
            .Where(si => si.CompanyId == companyId
                      && si.Status == Core.DocumentStatus.Posted
                      && si.OutstandingAmount > 0
                      && !si.IsReturn)
            .ToList();

        return BuildAgingReport(outstandingInvoices.Select(si => new AgingItem
        {
            PartyId = si.CustomerId,
            DocumentId = si.Id,
            DocumentNumber = si.InvoiceNumber,
            PostingDate = si.IssueDate,
            DueDate = si.DueDate ?? si.IssueDate,
            OutstandingAmount = si.OutstandingAmount,
        }), asOfDate, bucketDays, "Receivable");
    }

    /// <summary>
    /// Calculates AP aging (payables) for a company as of a given date.
    /// </summary>
    public async Task<AgingReport> CalculatePayablesAgingAsync(
        Guid companyId, DateTime asOfDate, int[] bucketDays = null!)
    {
        bucketDays ??= new[] { 30, 60, 90, 120 };

        var query = await _purchaseInvoiceRepository.GetQueryableAsync();
        var outstandingInvoices = query
            .Where(pi => pi.CompanyId == companyId
                      && pi.Status == Core.DocumentStatus.Posted
                      && pi.OutstandingAmount > 0
                      && !pi.IsReturn)
            .ToList();

        return BuildAgingReport(outstandingInvoices.Select(pi => new AgingItem
        {
            PartyId = pi.SupplierId,
            DocumentId = pi.Id,
            DocumentNumber = pi.InvoiceNumber,
            PostingDate = pi.IssueDate,
            DueDate = pi.DueDate ?? pi.IssueDate,
            OutstandingAmount = pi.OutstandingAmount,
        }), asOfDate, bucketDays, "Payable");
    }

    private static AgingReport BuildAgingReport(
        IEnumerable<AgingItem> items, DateTime asOfDate, int[] bucketDays, string reportType)
    {
        var report = new AgingReport
        {
            ReportType = reportType,
            AsOfDate = asOfDate,
            BucketRanges = bucketDays,
        };

        // Initialize buckets: [0-30], [31-60], [61-90], [91-120], [120+]
        var bucketCount = bucketDays.Length + 1;
        report.BucketTotals = new decimal[bucketCount];

        foreach (var item in items)
        {
            var ageDays = (int)(asOfDate - item.DueDate).TotalDays;
            if (ageDays < 0) ageDays = 0; // Not yet due

            var bucketIndex = GetBucketIndex(ageDays, bucketDays);
            report.BucketTotals[bucketIndex] += item.OutstandingAmount;
            report.TotalOutstanding += item.OutstandingAmount;
            report.InvoiceCount++;
        }

        return report;
    }

    private static int GetBucketIndex(int ageDays, int[] bucketDays)
    {
        for (int i = 0; i < bucketDays.Length; i++)
        {
            if (ageDays <= bucketDays[i]) return i;
        }
        return bucketDays.Length; // Last bucket (120+)
    }
}

public class AgingReport
{
    public string ReportType { get; set; } = null!;
    public DateTime AsOfDate { get; set; }
    public int[] BucketRanges { get; set; } = Array.Empty<int>();
    public decimal[] BucketTotals { get; set; } = Array.Empty<decimal>();
    public decimal TotalOutstanding { get; set; }
    public int InvoiceCount { get; set; }
}

public class AgingItem
{
    public Guid PartyId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal OutstandingAmount { get; set; }
}
