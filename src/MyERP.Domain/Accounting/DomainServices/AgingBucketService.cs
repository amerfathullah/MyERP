using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
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
    private readonly IRepository<CostCenter, Guid> _costCenterRepository;

    public AgingBucketService(
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<CostCenter, Guid> costCenterRepository)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _customerRepository = customerRepository;
        _supplierRepository = supplierRepository;
        _costCenterRepository = costCenterRepository;
    }

    /// <summary>
    /// Calculates AR aging (receivables) for a company as of a given date.
    /// Groups outstanding sales invoices into aging buckets.
    /// </summary>
    public async Task<AgingReport> CalculateReceivablesAgingAsync(
        Guid companyId,
        DateTime asOfDate,
        int[] bucketDays = null!,
        string calculateAgeingWith = "Report Date",
        string ageingBasedOn = "Due Date",
        Guid? partyId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? partyGroupId = null,
        bool includeRevaluationJournals = false)
    {
        bucketDays ??= new[] { 30, 60, 90, 120 };

        var query = await _salesInvoiceRepository.GetQueryableAsync();
        query = query.Where(si => si.CompanyId == companyId
                      && si.Status == Core.DocumentStatus.Posted
                      && (si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance) > 0
                      && !si.IsReturn);

        if (partyId.HasValue)
        {
            query = query.Where(si => si.CustomerId == partyId.Value);
        }
        if (partyGroupId.HasValue)
        {
            var custQuery = await _customerRepository.GetQueryableAsync();
            var customerIdsInGroup = custQuery
                .Where(c => c.CompanyId == companyId && c.CustomerGroupId == partyGroupId.Value)
                .Select(c => c.Id)
                .ToList();
            query = query.Where(si => customerIdsInGroup.Contains(si.CustomerId));
        }
        if (fromDate.HasValue)
        {
            query = query.Where(si => si.IssueDate >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            query = query.Where(si => si.IssueDate <= toDate.Value);
        }

        var outstandingInvoices = query.ToList();

        // Resolve customer names and customer group names for detailed report
        var customerIds = outstandingInvoices.Select(si => si.CustomerId).Distinct().ToList();
        var customerQuery = await _customerRepository.GetQueryableAsync();
        var customerData = customerQuery
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.CustomerGroupId })
            .ToList();
        var customerNames = customerData.ToDictionary(c => c.Id, c => c.Name);
        var customerGroupIds = customerData
            .Where(c => c.CustomerGroupId.HasValue)
            .Select(c => c.CustomerGroupId!.Value)
            .Distinct()
            .ToList();
        var customerGroupNames = new Dictionary<Guid, string>();
        if (customerGroupIds.Count > 0)
        {
            var groupRepo = LazyServiceProvider.LazyGetService<IRepository<CustomerGroup, Guid>>();
            if (groupRepo != null)
            {
                var groupQuery = await groupRepo.GetQueryableAsync();
                customerGroupNames = groupQuery
                    .Where(g => customerGroupIds.Contains(g.Id))
                    .Select(g => new { g.Id, g.Name })
                    .ToDictionary(g => g.Id, g => g.Name);
            }
        }
        var customerGroupMap = customerData.ToDictionary(
            c => c.Id,
            c => (
                GroupId: c.CustomerGroupId,
                GroupName: c.CustomerGroupId.HasValue ? customerGroupNames.GetValueOrDefault(c.CustomerGroupId.Value) : null
            ));

        // Resolve cost center names for detailed report (per ERPNext PR #58453)
        var costCenterIds = outstandingInvoices
            .Where(si => si.CostCenterId.HasValue)
            .Select(si => si.CostCenterId!.Value)
            .Distinct()
            .ToList();
        var costCenterNames = new Dictionary<Guid, string>();
        if (costCenterIds.Count > 0)
        {
            var costCenterQuery = await _costCenterRepository.GetQueryableAsync();
            costCenterNames = costCenterQuery
                .Where(cc => costCenterIds.Contains(cc.Id))
                .Select(cc => new { cc.Id, cc.Name })
                .ToDictionary(cc => cc.Id, cc => cc.Name);
        }

        var report = BuildAgingReport(outstandingInvoices.Select(si => new AgingItem
        {
            PartyId = si.CustomerId,
            PartyName = customerNames.GetValueOrDefault(si.CustomerId),
            PartyGroupId = customerGroupMap.TryGetValue(si.CustomerId, out var cg) ? cg.GroupId : null,
            PartyGroupName = customerGroupMap.TryGetValue(si.CustomerId, out var cg2) ? cg2.GroupName : null,
            DocumentId = si.Id,
            DocumentNumber = si.InvoiceNumber,
            PostingDate = si.IssueDate,
            DueDate = si.DueDate ?? si.IssueDate,
            InvoicedAmount = si.GrandTotal,
            PaidAmount = si.AmountPaid,
            AdvanceAmount = si.TotalAdvance,
            OutstandingAmount = si.OutstandingAmount,
            IsInvoice = true,
            CostCenterId = si.CostCenterId,
            CostCenterName = si.CostCenterId.HasValue ? costCenterNames.GetValueOrDefault(si.CostCenterId.Value) : null,
        }), asOfDate, bucketDays, "Receivable", calculateAgeingWith, ageingBasedOn, includeRevaluationJournals);

        return report;
    }

    /// <summary>
    /// Calculates AP aging (payables) for a company as of a given date.
    /// </summary>
    public async Task<AgingReport> CalculatePayablesAgingAsync(
        Guid companyId,
        DateTime asOfDate,
        int[] bucketDays = null!,
        string calculateAgeingWith = "Report Date",
        string ageingBasedOn = "Due Date",
        Guid? partyId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? partyGroupId = null,
        bool includeRevaluationJournals = false)
    {
        bucketDays ??= new[] { 30, 60, 90, 120 };

        var query = await _purchaseInvoiceRepository.GetQueryableAsync();
        query = query.Where(pi => pi.CompanyId == companyId
                      && pi.Status == Core.DocumentStatus.Posted
                      && (pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance) > 0
                      && !pi.IsReturn);

        if (partyId.HasValue)
        {
            query = query.Where(pi => pi.SupplierId == partyId.Value);
        }
        if (partyGroupId.HasValue)
        {
            var suppQuery = await _supplierRepository.GetQueryableAsync();
            var supplierIdsInGroup = suppQuery
                .Where(s => s.CompanyId == companyId && s.SupplierGroupId == partyGroupId.Value)
                .Select(s => s.Id)
                .ToList();
            query = query.Where(pi => supplierIdsInGroup.Contains(pi.SupplierId));
        }
        if (fromDate.HasValue)
        {
            query = query.Where(pi => pi.IssueDate >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            query = query.Where(pi => pi.IssueDate <= toDate.Value);
        }

        var outstandingInvoices = query.ToList();

        // Resolve supplier names and supplier group names for detailed report
        var supplierIds = outstandingInvoices.Select(pi => pi.SupplierId).Distinct().ToList();
        var supplierQuery = await _supplierRepository.GetQueryableAsync();
        var supplierData = supplierQuery
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.SupplierGroupId })
            .ToList();
        var supplierNames = supplierData.ToDictionary(s => s.Id, s => s.Name);
        var supplierGroupIds = supplierData
            .Where(s => s.SupplierGroupId.HasValue)
            .Select(s => s.SupplierGroupId!.Value)
            .Distinct()
            .ToList();
        var supplierGroupNames = new Dictionary<Guid, string>();
        if (supplierGroupIds.Count > 0)
        {
            var groupRepo = LazyServiceProvider.LazyGetService<IRepository<SupplierGroup, Guid>>();
            if (groupRepo != null)
            {
                var groupQuery = await groupRepo.GetQueryableAsync();
                supplierGroupNames = groupQuery
                    .Where(g => supplierGroupIds.Contains(g.Id))
                    .Select(g => new { g.Id, g.Name })
                    .ToDictionary(g => g.Id, g => g.Name);
            }
        }
        var supplierGroupMap = supplierData.ToDictionary(
            s => s.Id,
            s => (
                GroupId: s.SupplierGroupId,
                GroupName: s.SupplierGroupId.HasValue ? supplierGroupNames.GetValueOrDefault(s.SupplierGroupId.Value) : null
            ));

        // Resolve cost center names for detailed report (per ERPNext PR #58453)
        var costCenterIds = outstandingInvoices
            .Where(pi => pi.CostCenterId.HasValue)
            .Select(pi => pi.CostCenterId!.Value)
            .Distinct()
            .ToList();
        var costCenterNames = new Dictionary<Guid, string>();
        if (costCenterIds.Count > 0)
        {
            var costCenterQuery = await _costCenterRepository.GetQueryableAsync();
            costCenterNames = costCenterQuery
                .Where(cc => costCenterIds.Contains(cc.Id))
                .Select(cc => new { cc.Id, cc.Name })
                .ToDictionary(cc => cc.Id, cc => cc.Name);
        }

        return BuildAgingReport(outstandingInvoices.Select(pi => new AgingItem
        {
            PartyId = pi.SupplierId,
            PartyName = supplierNames.GetValueOrDefault(pi.SupplierId),
            PartyGroupId = supplierGroupMap.TryGetValue(pi.SupplierId, out var sg) ? sg.GroupId : null,
            PartyGroupName = supplierGroupMap.TryGetValue(pi.SupplierId, out var sg2) ? sg2.GroupName : null,
            DocumentId = pi.Id,
            DocumentNumber = pi.InvoiceNumber,
            PostingDate = pi.IssueDate,
            DueDate = pi.DueDate ?? pi.IssueDate,
            InvoicedAmount = pi.GrandTotal,
            PaidAmount = pi.AmountPaid,
            AdvanceAmount = pi.TotalAdvance,
            OutstandingAmount = pi.OutstandingAmount,
            IsInvoice = true,
            CostCenterId = pi.CostCenterId,
            CostCenterName = pi.CostCenterId.HasValue ? costCenterNames.GetValueOrDefault(pi.CostCenterId.Value) : null,
        }), asOfDate, bucketDays, "Payable", calculateAgeingWith, ageingBasedOn, includeRevaluationJournals);
    }

    /// <summary>
    /// Calculates unallocated credit on a non-invoice voucher (e.g. Journal Entry or Payment Entry).
    /// Per ERPNext PR #60040 / commit 66c9332582:
    /// Payments allocated against debt booked by a Journal Entry land on that entry's row,
    /// so only the credit left after settling its own debit is an advance.
    /// </summary>
    public static decimal GetUnallocatedCredit(decimal paidAmount, decimal invoicedAmount)
    {
        return Math.Max(paidAmount - invoicedAmount, 0m);
    }

    private static AgingReport BuildAgingReport(
        IEnumerable<AgingItem> items,
        DateTime asOfDate,
        int[] bucketDays,
        string reportType,
        string calculateAgeingWith = "Report Date",
        string ageingBasedOn = "Due Date",
        bool includeRevaluationJournals = false)
    {
        var isPostingDate = string.Equals(ageingBasedOn, "Posting Date", StringComparison.OrdinalIgnoreCase);
        var report = new AgingReport
        {
            ReportType = reportType,
            AsOfDate = asOfDate,
            CalculateAgeingWith = calculateAgeingWith,
            AgeingBasedOn = isPostingDate ? "Posting Date" : "Due Date",
            BucketRanges = bucketDays,
            IncludeRevaluationJournals = includeRevaluationJournals,
        };

        // Initialize buckets: [0-30], [31-60], [61-90], [91-120], [120+]
        var bucketCount = bucketDays.Length + 1;
        report.BucketTotals = new decimal[bucketCount];
        var partyMap = new Dictionary<Guid, AgingPartySummary>();

        foreach (var item in items)
        {
            var baseDate = isPostingDate ? item.PostingDate : item.DueDate;
            var ageDays = (int)(asOfDate.Date - baseDate.Date).TotalDays;

            if (!partyMap.TryGetValue(item.PartyId, out var partySummary))
            {
                partySummary = new AgingPartySummary
                {
                    PartyId = item.PartyId,
                    PartyName = item.PartyName,
                    PartyGroupId = item.PartyGroupId,
                    PartyGroupName = item.PartyGroupName,
                    BucketTotals = new decimal[bucketCount],
                };
                partyMap[item.PartyId] = partySummary;
            }

            partySummary.PartyName ??= item.PartyName;
            partySummary.PartyGroupId ??= item.PartyGroupId;
            partySummary.PartyGroupName ??= item.PartyGroupName;
            partySummary.InvoiceCount++;
            partySummary.InvoicedAmount += item.InvoicedAmount;
            partySummary.OutstandingAmount += item.OutstandingAmount;

            if (!item.IsInvoice)
            {
                // Per ERPNext PR #60040 / commit 66c9332582:
                // non-invoice vouchers (e.g. Journal Entry / Payment Entry):
                // only unallocated credit (excess of paid over own invoiced/debit) is advance
                var unallocatedCredit = GetUnallocatedCredit(item.PaidAmount, item.InvoicedAmount);
                partySummary.AdvanceAmount += unallocatedCredit;
                partySummary.PaidAmount += item.PaidAmount - unallocatedCredit;
            }
            else
            {
                partySummary.AdvanceAmount += item.AdvanceAmount;
                partySummary.PaidAmount += item.PaidAmount;
            }

            if (ageDays < 0)
            {
                // Not yet due (ERPNext PR #60040 / commit 33e4614973: range0 / <0)
                report.NotYetDueAmount += item.OutstandingAmount;
                report.TotalOutstanding += item.OutstandingAmount;
                report.InvoiceCount++;

                partySummary.NotYetDueAmount += item.OutstandingAmount;

                report.Details.Add(new AgingDetailEntry
                {
                    PartyId = item.PartyId,
                    PartyName = item.PartyName,
                    PartyGroupId = item.PartyGroupId,
                    PartyGroupName = item.PartyGroupName,
                    DocumentId = item.DocumentId,
                    DocumentNumber = item.DocumentNumber,
                    PostingDate = item.PostingDate,
                    DueDate = item.DueDate,
                    OutstandingAmount = item.OutstandingAmount,
                    AgeDays = ageDays,
                    BucketIndex = -1,
                    BucketLabel = "<0 (Not Yet Due)",
                    CostCenterId = item.CostCenterId,
                    CostCenterName = item.CostCenterName,
                });
                continue;
            }

            var bucketIndex = GetBucketIndex(ageDays, bucketDays);
            report.BucketTotals[bucketIndex] += item.OutstandingAmount;
            report.TotalOutstanding += item.OutstandingAmount;
            report.InvoiceCount++;

            partySummary.BucketTotals[bucketIndex] += item.OutstandingAmount;

            // Add detail entry for the detailed report view
            report.Details.Add(new AgingDetailEntry
            {
                PartyId = item.PartyId,
                PartyName = item.PartyName,
                PartyGroupId = item.PartyGroupId,
                PartyGroupName = item.PartyGroupName,
                DocumentId = item.DocumentId,
                DocumentNumber = item.DocumentNumber,
                PostingDate = item.PostingDate,
                DueDate = item.DueDate,
                OutstandingAmount = item.OutstandingAmount,
                AgeDays = ageDays,
                BucketIndex = bucketIndex,
                BucketLabel = GetBucketLabel(bucketIndex, bucketDays),
                CostCenterId = item.CostCenterId,
                CostCenterName = item.CostCenterName,
            });
        }

        foreach (var p in partyMap.Values)
        {
            p.TotalDue = p.BucketTotals.Sum();
            report.PartySummaries.Add(p);
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
    public string CalculateAgeingWith { get; set; } = "Report Date";
    public string AgeingBasedOn { get; set; } = "Due Date";
    public int[] BucketRanges { get; set; } = Array.Empty<int>();
    public decimal[] BucketTotals { get; set; } = Array.Empty<decimal>();
    /// <summary>Amount not yet due (due date in future relative to asOfDate). Maps to ERPNext range0.</summary>
    public decimal NotYetDueAmount { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int InvoiceCount { get; set; }
    public bool IncludeRevaluationJournals { get; set; }

    /// <summary>
    /// Detailed per-invoice aging entries with bucket assignment.
    /// Per ERPNext AR/AP report: shows invoice-level detail with party grouping.
    /// </summary>
    public List<AgingDetailEntry> Details { get; set; } = new();

    /// <summary>
    /// Party-level summaries for Accounts Receivable / Payable Summary view.
    /// Per ERPNext PR #60040 (commit 66c9332582): unallocated credits count as advance.
    /// </summary>
    public List<AgingPartySummary> PartySummaries { get; set; } = new();
}

/// <summary>Per-party summary entry in an aging report (for Accounts Receivable / Payable Summary view).</summary>
public class AgingPartySummary
{
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }
    public Guid? PartyGroupId { get; set; }
    public string? PartyGroupName { get; set; }
    public decimal InvoicedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal CreditNoteAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal NotYetDueAmount { get; set; }
    public decimal[] BucketTotals { get; set; } = [];
    public decimal TotalDue { get; set; }
    public int InvoiceCount { get; set; }
}

/// <summary>Per-invoice detail entry in an aging report (for detailed AR/AP view).</summary>
public class AgingDetailEntry
{
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }
    public Guid? PartyGroupId { get; set; }
    public string? PartyGroupName { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int AgeDays { get; set; }
    public int BucketIndex { get; set; }
    public string BucketLabel { get; set; } = null!;
    public Guid? CostCenterId { get; set; }
    public string? CostCenterName { get; set; }
}

public class AgingItem
{
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }
    public Guid? PartyGroupId { get; set; }
    public string? PartyGroupName { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal InvoicedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public bool IsInvoice { get; set; } = true;
    public Guid? CostCenterId { get; set; }
    public string? CostCenterName { get; set; }
}
