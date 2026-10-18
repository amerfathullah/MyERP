using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Projects.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Implements Delivered Items to be Billed report matching ERPNext accounts/report/delivered_items_to_be_billed
/// and non_billed_report.py.
/// </summary>
[Authorize(MyERPPermissions.DeliveryNotes.Default)]
public class DeliveredItemsToBeBilledAppService : ApplicationService, IDeliveredItemsToBeBilledAppService
{
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo;
    private readonly IRepository<Project, Guid> _projectRepo;
    private readonly IRepository<Company, Guid> _companyRepo;

    public DeliveredItemsToBeBilledAppService(
        IRepository<DeliveryNote, Guid> deliveryNoteRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<SalesOrder, Guid> salesOrderRepo,
        IRepository<Project, Guid> projectRepo,
        IRepository<Company, Guid> companyRepo)
    {
        _deliveryNoteRepo = deliveryNoteRepo;
        _customerRepo = customerRepo;
        _itemRepo = itemRepo;
        _salesOrderRepo = salesOrderRepo;
        _projectRepo = projectRepo;
        _companyRepo = companyRepo;
    }

    /// <inheritdoc />
    public async Task<DeliveredItemsToBeBilledReportDto> GetReportAsync(GetDeliveredItemsToBeBilledInput input)
    {
        var company = await _companyRepo.FindAsync(input.CompanyId);
        var companyCurrency = company?.CurrencyCode ?? "MYR";
        var cutoffDate = input.PostingDate ?? DateTime.UtcNow.Date;

        var dnQuery = await _deliveryNoteRepo.WithDetailsAsync(d => d.Items);
        var deliveryNotes = dnQuery
            .Where(d => d.CompanyId == input.CompanyId
                        && d.Status != DocumentStatus.Draft
                        && d.Status != DocumentStatus.Cancelled
                        && d.Status != DocumentStatus.Closed
                        && !d.IsReturn
                        && d.PostingDate <= cutoffDate)
            .WhereIf(input.DeliveryNoteId.HasValue, d => d.Id == input.DeliveryNoteId!.Value)
            .WhereIf(input.CustomerId.HasValue, d => d.CustomerId == input.CustomerId!.Value)
            .ToList();

        if (deliveryNotes.Count == 0)
        {
            return new DeliveredItemsToBeBilledReportDto { Currency = companyCurrency };
        }

        // Exclude fully completed/billed Delivery Notes per ERPNext status.notin(["Closed", "Completed"])
        deliveryNotes = deliveryNotes.Where(d => d.BillingStatus != "Completed").ToList();

        if (deliveryNotes.Count == 0)
        {
            return new DeliveredItemsToBeBilledReportDto { Currency = companyCurrency };
        }

        var allItems = deliveryNotes
            .SelectMany(d => d.Items.Select(item => new { DeliveryNote = d, Item = item }))
            .Where(x => !x.Item.IsClosed && x.Item.Quantity > 0)
            .WhereIf(input.ItemId.HasValue, x => x.Item.ItemId == input.ItemId!.Value)
            .ToList();

        if (allItems.Count == 0)
        {
            return new DeliveredItemsToBeBilledReportDto { Currency = companyCurrency };
        }

        var itemIds = allItems.Select(x => x.Item.ItemId).Distinct().ToList();
        var itemQuery = await _itemRepo.GetQueryableAsync();
        var itemsMap = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionary(i => i.Id);

        var customerIds = deliveryNotes.Select(d => d.CustomerId).Distinct().ToList();
        var customerQuery = await _customerRepo.GetQueryableAsync();
        var customersMap = customerQuery
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => c.Name);

        var soIds = deliveryNotes.Where(d => d.SalesOrderId.HasValue).Select(d => d.SalesOrderId!.Value).Distinct().ToList();
        var soMap = new Dictionary<Guid, Guid?>();
        if (soIds.Count > 0)
        {
            var soQuery = await _salesOrderRepo.GetQueryableAsync();
            soMap = soQuery.Where(s => soIds.Contains(s.Id)).ToDictionary(s => s.Id, s => s.ProjectId);
        }

        var projectIds = soMap.Values.Where(p => p.HasValue).Select(p => p!.Value).Distinct().ToList();
        var projectMap = new Dictionary<Guid, string>();
        if (projectIds.Count > 0)
        {
            var projQuery = await _projectRepo.GetQueryableAsync();
            projectMap = projQuery.Where(p => projectIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p.ProjectName);
        }

        var rows = new List<DeliveredItemToBeBilledRowDto>();

        foreach (var line in allItems)
        {
            var dn = line.DeliveryNote;
            var item = line.Item;

            itemsMap.TryGetValue(item.ItemId, out var itemMaster);

            // Filter item.is_stock_item == 1 per ERPNext non_billed_report.py
            if (itemMaster != null && !itemMaster.MaintainStock)
            {
                continue;
            }

            var conversionRate = dn.ExchangeRate > 0 ? dn.ExchangeRate : 1m;
            var amount = Math.Round(item.LineTotal * conversionRate, 2);
            var billedAmount = Math.Round(item.BilledQty * item.UnitPrice * conversionRate, 2);
            var returnedAmount = Math.Round(item.ReturnedQty * item.UnitPrice * conversionRate, 2);
            var pendingAmount = Math.Round(amount - billedAmount - returnedAmount, 2);

            // ERPNext condition: pending_amount > 0
            if (pendingAmount <= 0)
            {
                continue;
            }

            customersMap.TryGetValue(dn.CustomerId, out var customerName);

            Guid? projectId = null;
            string? projectName = null;
            if (dn.SalesOrderId.HasValue && soMap.TryGetValue(dn.SalesOrderId.Value, out var pId) && pId.HasValue)
            {
                projectId = pId.Value;
                projectMap.TryGetValue(projectId.Value, out projectName);
            }

            var pendingQty = Math.Max(0, item.Quantity - item.BilledQty - item.ReturnedQty);

            rows.Add(new DeliveredItemToBeBilledRowDto
            {
                DeliveryNoteId = dn.Id,
                DeliveryNumber = dn.DeliveryNumber,
                PostingDate = dn.PostingDate,
                CustomerId = dn.CustomerId,
                CustomerName = customerName ?? string.Empty,
                ItemId = item.ItemId,
                ItemCode = itemMaster?.ItemCode ?? string.Empty,
                ItemName = itemMaster?.ItemName ?? string.Empty,
                Description = item.Description ?? string.Empty,
                DeliveredQty = item.Quantity,
                BilledQty = item.BilledQty,
                ReturnedQty = item.ReturnedQty,
                PendingQty = pendingQty,
                Rate = item.UnitPrice,
                Amount = amount,
                BilledAmount = billedAmount,
                ReturnedAmount = returnedAmount,
                PendingAmount = pendingAmount,
                ProjectId = projectId,
                ProjectName = projectName,
                Currency = companyCurrency
            });
        }

        // Order by PostingDate desc, DeliveryNumber desc matching ERPNext orderby
        rows = rows.OrderByDescending(r => r.PostingDate).ThenByDescending(r => r.DeliveryNumber).ToList();

        return new DeliveredItemsToBeBilledReportDto
        {
            Rows = rows,
            TotalAmount = rows.Sum(r => r.Amount),
            TotalBilledAmount = rows.Sum(r => r.BilledAmount),
            TotalReturnedAmount = rows.Sum(r => r.ReturnedAmount),
            TotalPendingAmount = rows.Sum(r => r.PendingAmount),
            Currency = companyCurrency
        };
    }
}
