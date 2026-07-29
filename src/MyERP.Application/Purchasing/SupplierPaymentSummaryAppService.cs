using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseInvoices.Default)]
public class SupplierPaymentSummaryAppService : ApplicationService
{
    private readonly IRepository<PurchaseInvoice, Guid> _invoiceRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;

    public SupplierPaymentSummaryAppService(
        IRepository<PurchaseInvoice, Guid> invoiceRepository,
        IRepository<Supplier, Guid> supplierRepository)
    {
        _invoiceRepository = invoiceRepository;
        _supplierRepository = supplierRepository;
    }

    /// <summary>
    /// Returns payment summary grouped by supplier for the given date range.
    /// Shows: total invoiced, total paid, outstanding, overdue count, payment timeliness.
    /// Per ERPNext: critical for AP management and supplier payment prioritization.
    /// </summary>
    public async Task<SupplierPaymentSummaryReportDto> GetReportAsync(RegisterFilterDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-3).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;
        var today = DateTime.UtcNow.Date;

        var query = await _invoiceRepository.GetQueryableAsync();
        var invoices = query
            .Where(pi => pi.CompanyId == input.CompanyId
                      && pi.Status == DocumentStatus.Posted
                      && !pi.IsReturn
                      && pi.IssueDate >= from
                      && pi.IssueDate <= to)
            .ToList();

        // Load supplier names
        var supplierIds = invoices.Select(pi => pi.SupplierId).Distinct().ToList();
        var supplierQuery = await _supplierRepository.GetQueryableAsync();
        var supplierNames = supplierQuery
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionary(s => s.Id, s => s.Name);

        var supplierGroups = invoices
            .GroupBy(pi => pi.SupplierId)
            .Select(g =>
            {
                var items = g.ToList();
                var totalInvoiced = items.Sum(pi => pi.GrandTotal);
                var totalPaid = items.Sum(pi => pi.AmountPaid);
                var totalOutstanding = items.Sum(pi => pi.OutstandingAmount);
                var overdueCount = items.Count(pi =>
                    pi.OutstandingAmount > 0.01m && pi.DueDate.HasValue && pi.DueDate.Value < today);
                var overdueAmount = items
                    .Where(pi => pi.OutstandingAmount > 0.01m && pi.DueDate.HasValue && pi.DueDate.Value < today)
                    .Sum(pi => pi.OutstandingAmount);
                var totalInvoices = items.Count;
                var paidOnTime = items.Count(pi =>
                    pi.AmountPaid >= pi.GrandTotal * 0.99m && pi.DueDate.HasValue);
                var withDueDate = items.Count(pi => pi.DueDate.HasValue);

                return new SupplierPaymentLineDto
                {
                    SupplierId = g.Key,
                    SupplierName = supplierNames.GetValueOrDefault(g.Key, "—"),
                    InvoiceCount = totalInvoices,
                    TotalInvoiced = totalInvoiced,
                    TotalPaid = totalPaid,
                    TotalOutstanding = totalOutstanding,
                    OverdueCount = overdueCount,
                    OverdueAmount = overdueAmount,
                    PaymentTimeliness = withDueDate > 0 ? (decimal)paidOnTime / withDueDate * 100 : 100,
                };
            })
            .OrderByDescending(x => x.TotalOutstanding)
            .ToList();

        return new SupplierPaymentSummaryReportDto
        {
            Items = supplierGroups,
            TotalInvoiced = supplierGroups.Sum(x => x.TotalInvoiced),
            TotalPaid = supplierGroups.Sum(x => x.TotalPaid),
            TotalOutstanding = supplierGroups.Sum(x => x.TotalOutstanding),
            TotalOverdueAmount = supplierGroups.Sum(x => x.OverdueAmount),
            SupplierCount = supplierGroups.Count,
        };
    }
}

// DTOs
public class SupplierPaymentSummaryReportDto
{
    public List<SupplierPaymentLineDto> Items { get; set; } = new();
    public decimal TotalInvoiced { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal TotalOverdueAmount { get; set; }
    public int SupplierCount { get; set; }
}

public class SupplierPaymentLineDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
    public int InvoiceCount { get; set; }
    public decimal TotalInvoiced { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int OverdueCount { get; set; }
    public decimal OverdueAmount { get; set; }
    public decimal PaymentTimeliness { get; set; } // Percentage paid on time
}
