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
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;

    public AgingBucketService(
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Supplier, Guid> supplierRepository)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _customerRepository = customerRepository;
        _supplierRepository = supplierRepository;
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
                      && (si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance) > 0
                      && !si.IsReturn)
            .ToList();

        // Resolve customer names for detailed report
        var customerIds = outstandingInvoices.Select(si => si.CustomerId).Distinct().ToList();
        var customerQuery = await _customerRepository.GetQueryableAsync();
        var customerNames = customerQuery
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionary(c => c.Id, c => c.Name);

        var report = BuildAgingReport(outstandingInvoices.Select(si => new AgingItem
        {
            PartyId = si.CustomerId,
            PartyName = customerNames.GetValueOrDefault(si.CustomerId),
            DocumentId = si.Id,
            DocumentNumber = si.InvoiceNumber,
            PostingDate = si.IssueDate,
            DueDate = si.DueDate ?? si.IssueDate,
            OutstandingAmount = si.OutstandingAmount,
        }), asOfDate, bucketDays, "Receivable");

        return report;
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
                      && (pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance) > 0
                      && !pi.IsReturn)
            .ToList();

        // Resolve supplier names for detailed report
        var supplierIds = outstandingInvoices.Select(pi => pi.SupplierId).Distinct().ToList();
        var supplierQuery = await _supplierRepository.GetQueryableAsync();
        var supplierNames = supplierQuery
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionary(s => s.Id, s => s.Name);

        return BuildAgingReport(outstandingInvoices.Select(pi => new AgingItem
        {
            PartyId = pi.SupplierId,
            PartyName = supplierNames.GetValueOrDefault(pi.SupplierId),
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

            // Add detail entry for the detailed report view
            report.Details.Add(new AgingDetailEntry
            {
                PartyId = item.PartyId,
                PartyName = item.PartyName,
                DocumentId = item.DocumentId,
                DocumentNumber = item.DocumentNumber,
                PostingDate = item.PostingDate,
                DueDate = item.DueDate,
                OutstandingAmount = item.OutstandingAmount,
                AgeDays = ageDays,
                BucketIndex = bucketIndex,
                BucketLabel = GetBucketLabel(bucketIndex, bucketDays),
            });
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

    private static string GetBucketLabel(int bucketIndex, int[] bucketDays)
    {
        if (bucketIndex == 0)
            return $"0-{bucketDays[0]}";
        if (bucketIndex < bucketDays.Length)
            return $"{bucketDays[bucketIndex - 1] + 1}-{bucketDays[bucketIndex]}";
        return $"{bucketDays[^1] + 1}+";
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

    /// <summary>
    /// Detailed per-invoice aging entries with bucket assignment.
    /// Per ERPNext AR/AP report: shows invoice-level detail with party grouping.
    /// </summary>
    public List<AgingDetailEntry> Details { get; set; } = new();
}

/// <summary>Per-invoice detail entry in an aging report (for detailed AR/AP view).</summary>
public class AgingDetailEntry
{
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int AgeDays { get; set; }
    public int BucketIndex { get; set; }
    public string BucketLabel { get; set; } = null!;
}

public class AgingItem
{
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal OutstandingAmount { get; set; }
}
