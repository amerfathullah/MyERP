using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Shows upcoming supplier payments due in the next N days, grouped by week.
/// Per ERPNext: Accounts Payable report filtered for future due dates — helps treasury plan cash outflows.
/// </summary>
public class UpcomingPaymentDueDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public DateTime DueDate { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? CurrencyCode { get; set; }
    public int DaysUntilDue { get; set; }
    public string WeekLabel { get; set; } = null!;
    public bool IsOverdue { get; set; }
}

public class UpcomingPaymentsDueReportDto
{
    public decimal TotalDueThisWeek { get; set; }
    public decimal TotalDueNextWeek { get; set; }
    public decimal TotalDueNext30Days { get; set; }
    public decimal TotalOverdue { get; set; }
    public int InvoiceCount { get; set; }
    public int SupplierCount { get; set; }
    public List<UpcomingPaymentDueDto> Invoices { get; set; } = [];
}

public class GetUpcomingPaymentsDueInput
{
    public Guid CompanyId { get; set; }
    public int DaysAhead { get; set; } = 30;
    public Guid? SupplierId { get; set; }
}

[Authorize(MyERPPermissions.PurchaseInvoices.Default)]
public class UpcomingPaymentsDueAppService : ApplicationService
{
    private readonly IRepository<PurchaseInvoice, Guid> _piRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;

    public UpcomingPaymentsDueAppService(
        IRepository<PurchaseInvoice, Guid> piRepo,
        IRepository<Supplier, Guid> supplierRepo)
    {
        _piRepo = piRepo;
        _supplierRepo = supplierRepo;
    }

    public async Task<UpcomingPaymentsDueReportDto> GetReportAsync(GetUpcomingPaymentsDueInput input)
    {
        var today = DateTime.UtcNow.Date;
        var endDate = today.AddDays(input.DaysAhead);

        // Get posted invoices with outstanding balance, due within window (or overdue)
        var queryable = await _piRepo.GetQueryableAsync();
        var invoices = queryable
            .Where(pi => pi.CompanyId == input.CompanyId
                         && pi.Status == Core.DocumentStatus.Posted
                         && !pi.IsReturn
                         && (pi.GrandTotal - pi.AmountPaid) > 0.01m
                         && pi.DueDate != null
                         && pi.DueDate <= endDate)
            .Select(pi => new
            {
                pi.Id,
                pi.InvoiceNumber,
                pi.SupplierId,
                pi.DueDate,
                pi.GrandTotal,
                pi.AmountPaid,
                pi.CurrencyCode,
            })
            .ToList();

        if (input.SupplierId.HasValue)
        {
            invoices = invoices.Where(i => i.SupplierId == input.SupplierId.Value).ToList();
        }

        // Resolve supplier names
        var supplierIds = invoices.Select(i => i.SupplierId).Distinct().ToList();
        var supplierQueryable = await _supplierRepo.GetQueryableAsync();
        var supplierNames = supplierQueryable
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToList()
            .ToDictionary(s => s.Id, s => s.Name);

        // Map to DTOs with week grouping
        var endOfThisWeek = today.AddDays(7 - (int)today.DayOfWeek);
        var endOfNextWeek = endOfThisWeek.AddDays(7);

        var result = new UpcomingPaymentsDueReportDto();
        var items = new List<UpcomingPaymentDueDto>();

        foreach (var inv in invoices.OrderBy(i => i.DueDate))
        {
            var outstanding = inv.GrandTotal - inv.AmountPaid;
            var dueDate = inv.DueDate!.Value;
            var daysUntilDue = (dueDate - today).Days;
            var isOverdue = dueDate < today;

            string weekLabel;
            if (isOverdue)
            {
                weekLabel = "Overdue";
                result.TotalOverdue += outstanding;
            }
            else if (dueDate <= endOfThisWeek)
            {
                weekLabel = "This Week";
                result.TotalDueThisWeek += outstanding;
            }
            else if (dueDate <= endOfNextWeek)
            {
                weekLabel = "Next Week";
                result.TotalDueNextWeek += outstanding;
            }
            else
            {
                weekLabel = $"Week of {dueDate.AddDays(-(int)dueDate.DayOfWeek):dd MMM}";
            }

            result.TotalDueNext30Days += outstanding;

            items.Add(new UpcomingPaymentDueDto
            {
                InvoiceId = inv.Id,
                InvoiceNumber = inv.InvoiceNumber ?? inv.Id.ToString()[..8],
                SupplierId = inv.SupplierId,
                SupplierName = supplierNames.GetValueOrDefault(inv.SupplierId, "—"),
                DueDate = dueDate,
                OutstandingAmount = outstanding,
                GrandTotal = inv.GrandTotal,
                CurrencyCode = inv.CurrencyCode,
                DaysUntilDue = daysUntilDue,
                WeekLabel = weekLabel,
                IsOverdue = isOverdue,
            });
        }

        result.Invoices = items;
        result.InvoiceCount = items.Count;
        result.SupplierCount = supplierIds.Count;

        return result;
    }
}
