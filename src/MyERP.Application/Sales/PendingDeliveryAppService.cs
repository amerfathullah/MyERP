using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Pending Delivery Report — shows what's promised to customers and when.
/// Critical for operations/warehouse daily planning.
/// 
/// ERPNext equivalent: selling/report/pending_so_items_for_purchase_and_transfer
/// </summary>
public class PendingDeliveryItemDto
{
    public Guid SalesOrderId { get; set; }
    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public decimal OrderedQty { get; set; }
    public decimal DeliveredQty { get; set; }
    public decimal PendingQty { get; set; }
    public string Uom { get; set; } = null!;
    public decimal Rate { get; set; }
    public decimal PendingAmount { get; set; }
    public int DaysUntilDue { get; set; }
    public bool IsOverdue { get; set; }
    public string? WarehouseId { get; set; }
}

public class PendingDeliveryReportDto
{
    public DateTime AsOfDate { get; set; }
    public int TotalOrders { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalPendingAmount { get; set; }
    public int OverdueCount { get; set; }
    public decimal OverdueAmount { get; set; }
    public List<PendingDeliveryItemDto> Items { get; set; } = [];
}

public class PendingDeliveryRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime? AsOfDate { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? ItemId { get; set; }
    public bool OverdueOnly { get; set; }
}

[Authorize(MyERPPermissions.SalesOrders.Default)]
public class PendingDeliveryAppService : ApplicationService
{
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Inventory.Entities.Item, Guid> _itemRepository;

    public PendingDeliveryAppService(
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Inventory.Entities.Item, Guid> itemRepository)
    {
        _salesOrderRepository = salesOrderRepository;
        _customerRepository = customerRepository;
        _itemRepository = itemRepository;
    }

    public async Task<PendingDeliveryReportDto> GetReportAsync(PendingDeliveryRequestDto input)
    {
        var asOfDate = input.AsOfDate ?? DateTime.UtcNow.Date;

        // Query active Sales Orders with pending delivery (non-Draft, non-Cancelled, non-Completed, non-Closed)
        var soQuery = await _salesOrderRepository.GetQueryableAsync();
        var activeOrders = soQuery
            .Where(so => so.CompanyId == input.CompanyId
                         && so.Status != DocumentStatus.Draft
                         && so.Status != DocumentStatus.Cancelled
                         && so.Status != (DocumentStatus)14 // Closed
                         && so.Status != (DocumentStatus)13) // Completed
            .Where(so => !input.CustomerId.HasValue || so.CustomerId == input.CustomerId.Value)
            .OrderBy(so => so.DeliveryDate ?? so.OrderDate)
            .Take(200)
            .ToList();

        // Resolve customer + item names in batch
        var customerIds = activeOrders.Select(so => so.CustomerId).Distinct().ToList();
        var customerQuery = await _customerRepository.GetQueryableAsync();
        var customerNames = customerQuery
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionary(c => c.Id, c => c.Name);

        var allItems = activeOrders.SelectMany(so => so.Items).ToList();
        var itemIds = allItems.Select(i => i.ItemId).Distinct().ToList();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var itemInfo = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName })
            .ToDictionary(i => i.Id, i => new { i.ItemCode, i.ItemName });

        // Build result items — only items with pending delivery qty
        var resultItems = new List<PendingDeliveryItemDto>();
        foreach (var so in activeOrders)
        {
            foreach (var item in so.Items)
            {
                var pendingQty = item.Quantity - item.DeliveredQty;
                if (pendingQty <= 0) continue;

                if (input.ItemId.HasValue && item.ItemId != input.ItemId.Value) continue;

                var deliveryDate = so.DeliveryDate ?? so.OrderDate.AddDays(7);
                var isOverdue = deliveryDate < asOfDate;
                if (input.OverdueOnly && !isOverdue) continue;

                var itemDetail = itemInfo.GetValueOrDefault(item.ItemId);
                resultItems.Add(new PendingDeliveryItemDto
                {
                    SalesOrderId = so.Id,
                    OrderNumber = so.OrderNumber ?? so.Id.ToString()[..8],
                    OrderDate = so.OrderDate,
                    DeliveryDate = deliveryDate,
                    CustomerId = so.CustomerId,
                    CustomerName = customerNames.GetValueOrDefault(so.CustomerId, "—"),
                    ItemId = item.ItemId,
                    ItemCode = itemDetail?.ItemCode ?? "—",
                    ItemName = itemDetail?.ItemName ?? "—",
                    OrderedQty = item.Quantity,
                    DeliveredQty = item.DeliveredQty,
                    PendingQty = pendingQty,
                    Uom = item.StockUom ?? "Unit",
                    Rate = item.UnitPrice,
                    PendingAmount = pendingQty * item.UnitPrice,
                    DaysUntilDue = (int)(deliveryDate - asOfDate).TotalDays,
                    IsOverdue = isOverdue,
                    WarehouseId = item.WarehouseId?.ToString()
                });
            }
        }

        // Sort: overdue first (by days overdue desc), then by delivery date asc
        resultItems = resultItems
            .OrderByDescending(i => i.IsOverdue)
            .ThenBy(i => i.DeliveryDate)
            .ToList();

        var overdueItems = resultItems.Where(i => i.IsOverdue).ToList();

        return new PendingDeliveryReportDto
        {
            AsOfDate = asOfDate,
            TotalOrders = resultItems.Select(i => i.SalesOrderId).Distinct().Count(),
            TotalItems = resultItems.Count,
            TotalPendingAmount = resultItems.Sum(i => i.PendingAmount),
            OverdueCount = overdueItems.Count,
            OverdueAmount = overdueItems.Sum(i => i.PendingAmount),
            Items = resultItems
        };
    }

    /// <summary>
    /// Creates a Delivery Note from selected pending items (grouped by customer).
    /// Per ERPNext: one DN per customer, items from multiple SOs allowed.
    /// </summary>
    [Authorize(MyERPPermissions.DeliveryNotes.Create)]
    public async Task<CreateDeliveryNoteResultDto> CreateDeliveryNoteAsync(CreateDnFromPendingDto input)
    {
        if (input.Items == null || input.Items.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        // Resolve warehouse: use first available from SO items
        var firstSoId = input.Items.First().SalesOrderId;
        var soQuery = await _salesOrderRepository.GetQueryableAsync();
        var firstSo = soQuery.FirstOrDefault(so => so.Id == firstSoId);
        var warehouseId = firstSo?.Items.FirstOrDefault(i => i.WarehouseId.HasValue)?.WarehouseId
                          ?? Guid.Empty;

        if (warehouseId == Guid.Empty)
            throw new BusinessException("MyERP:07005")
                .WithData("reason", "No warehouse configured on source Sales Order items. Please assign a warehouse.");

        // Create Delivery Note entity
        var numberGenerator = LazyServiceProvider.LazyGetRequiredService<IDocumentNumberGenerator>();
        var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>();

        var deliveryNumber = await numberGenerator.GenerateAsync("DeliveryNote", input.CompanyId);
        var dn = new DeliveryNote(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId,
            warehouseId,
            deliveryNumber,
            DateTime.UtcNow.Date,
            CurrentTenant.Id
        );

        // Add items from selected pending SO items — cap at pending qty
        foreach (var selItem in input.Items)
        {
            var so = soQuery.FirstOrDefault(s => s.Id == selItem.SalesOrderId);
            if (so == null) continue;

            var soItem = so.Items.FirstOrDefault(i => i.ItemId == selItem.ItemId);
            if (soItem == null) continue;

            var pendingQty = soItem.Quantity - soItem.DeliveredQty;
            var deliverQty = Math.Min(selItem.Quantity, pendingQty);
            if (deliverQty <= 0) continue;

            dn.AddItem(
                soItem.ItemId,
                soItem.Description ?? "—",
                deliverQty,
                soItem.UnitPrice,
                0m, // taxAmount resolved on submit
                soItem.StockUom ?? "Unit",
                soItem.Id // salesOrderItemId for fulfillment tracking
            );

            // Track UOM conversion for stock operations
            var lastItem = dn.Items.Last();
            lastItem.StockUom = soItem.StockUom ?? "Unit";
            lastItem.ConversionFactor = soItem.ConversionFactor;
        }

        if (dn.Items.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems)
                .WithData("reason", "All selected items are already fully delivered.");

        await dnRepo.InsertAsync(dn);

        return new CreateDeliveryNoteResultDto
        {
            DeliveryNoteId = dn.Id,
            DeliveryNumber = deliveryNumber,
            ItemCount = dn.Items.Count,
            TotalAmount = dn.Items.Sum(i => i.Quantity * i.UnitPrice)
        };
    }
}

public class CreateDnFromPendingDto
{
    public Guid CustomerId { get; set; }
    public Guid CompanyId { get; set; }
    public List<PendingItemSelectionDto> Items { get; set; } = [];
}

public class PendingItemSelectionDto
{
    public Guid SalesOrderId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
}

public class CreateDeliveryNoteResultDto
{
    public Guid DeliveryNoteId { get; set; }
    public string DeliveryNumber { get; set; } = null!;
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
}
