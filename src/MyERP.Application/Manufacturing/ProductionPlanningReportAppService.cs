using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

/// <summary>
/// Production Planning Report AppService.
/// Analyzes raw material requirements, warehouse stock allotment, shortages, and open PO arrivals for open production orders.
/// Maps to ERPNext manufacturing/report/production_planning_report/production_planning_report.py.
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class ProductionPlanningReportAppService : ApplicationService, IProductionPlanningReportAppService
{
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<MaterialRequest, Guid> _materialRequestRepository;
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public ProductionPlanningReportAppService(
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<MaterialRequest, Guid> materialRequestRepository,
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<PurchaseOrder, Guid> purchaseOrderRepository,
        IRepository<Bin, Guid> binRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _workOrderRepository = workOrderRepository;
        _salesOrderRepository = salesOrderRepository;
        _materialRequestRepository = materialRequestRepository;
        _bomRepository = bomRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _binRepository = binRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<ProductionPlanningReportDto> GetReportAsync(ProductionPlanningReportFilterDto input)
    {
        var items = await _itemRepository.GetListAsync();
        var itemMap = items.ToDictionary(i => i.Id);

        var warehouses = await _warehouseRepository.GetListAsync();
        var warehouseMap = warehouses.ToDictionary(w => w.Id);

        var bins = await _binRepository.GetListAsync();
        // Simulation tracking dictionary for stock allotment: (ItemId, WarehouseId) -> available actual qty
        var binTracker = bins.ToDictionary(b => (b.ItemId, b.WarehouseId), b => Math.Max(0m, b.ActualQty));

        // Get open Purchase Orders for expected arrival tracking
        var poQuery = await _purchaseOrderRepository.WithDetailsAsync(p => p.Items);
        var openPOs = poQuery
            .Where(p => p.Status != DocumentStatus.Draft &&
                        p.Status != DocumentStatus.Cancelled &&
                        p.Status != DocumentStatus.Closed &&
                        p.Status != DocumentStatus.Completed &&
                        p.PerReceived < 100m)
            .ToList();

        if (input.CompanyId.HasValue)
        {
            openPOs = openPOs.Where(p => p.CompanyId == input.CompanyId.Value).ToList();
        }

        var rows = new List<ProductionPlanningReportRowDto>();

        var basedOn = string.IsNullOrWhiteSpace(input.BasedOn) ? "WorkOrder" : input.BasedOn.Trim();

        if (basedOn.Equals("WorkOrder", StringComparison.OrdinalIgnoreCase))
        {
            await ProcessWorkOrdersAsync(input, itemMap, warehouseMap, binTracker, openPOs, rows);
        }
        else if (basedOn.Equals("SalesOrder", StringComparison.OrdinalIgnoreCase))
        {
            await ProcessSalesOrdersAsync(input, itemMap, warehouseMap, binTracker, openPOs, rows);
        }
        else if (basedOn.Equals("MaterialRequest", StringComparison.OrdinalIgnoreCase))
        {
            await ProcessMaterialRequestsAsync(input, itemMap, warehouseMap, binTracker, openPOs, rows);
        }

        // Apply filters
        if (input.RawMaterialWarehouseId.HasValue)
        {
            var targetWhName = warehouseMap.TryGetValue(input.RawMaterialWarehouseId.Value, out var wh) ? wh.Name : null;
            if (targetWhName != null)
            {
                rows = rows.Where(r => r.RawMaterialWarehouse.Equals(targetWhName, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        if (input.ItemId.HasValue && itemMap.TryGetValue(input.ItemId.Value, out var targetItem))
        {
            rows = rows.Where(r => r.ProductionItemCode.Equals(targetItem.ItemCode, StringComparison.OrdinalIgnoreCase)
                                || r.RawMaterialCode.Equals(targetItem.ItemCode, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim();
            rows = rows.Where(r =>
                r.OrderNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.ProductionItemCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.ProductionItemName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.RawMaterialCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.RawMaterialName.Contains(term, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        var report = new ProductionPlanningReportDto
        {
            TotalOrdersCount = rows.Select(r => r.OrderNo).Where(n => !string.IsNullOrEmpty(n)).Distinct().Count(),
            TotalRawMaterialsCount = rows.Select(r => r.RawMaterialCode).Where(c => !string.IsNullOrEmpty(c)).Distinct().Count(),
            TotalRequiredQty = rows.Sum(r => r.RequiredQty),
            TotalAllottedQty = rows.Sum(r => r.AllottedQty),
            TotalShortageQty = rows.Sum(r => r.ShortageQty),
            TotalExpectedArrivalQty = rows.Sum(r => r.ExpectedArrivalQty),
            Rows = rows
        };

        return report;
    }

    private async Task ProcessWorkOrdersAsync(
        ProductionPlanningReportFilterDto input,
        Dictionary<Guid, Item> itemMap,
        Dictionary<Guid, Warehouse> warehouseMap,
        Dictionary<(Guid, Guid), decimal> binTracker,
        List<PurchaseOrder> openPOs,
        List<ProductionPlanningReportRowDto> rows)
    {
        var woQuery = await _workOrderRepository.WithDetailsAsync(w => w.RequiredItems);
        var workOrders = woQuery
            .Where(w => w.Status != WorkOrderStatus.Completed &&
                        w.Status != WorkOrderStatus.Cancelled &&
                        w.Status != WorkOrderStatus.Closed &&
                        w.Status != WorkOrderStatus.Stopped &&
                        w.Status != WorkOrderStatus.Draft)
            .OrderBy(w => w.PlannedStartDate)
            .ToList();

        if (input.CompanyId.HasValue)
        {
            workOrders = workOrders.Where(w => w.CompanyId == input.CompanyId.Value).ToList();
        }

        foreach (var wo in workOrders)
        {
            var prodItem = itemMap.TryGetValue(wo.ItemId, out var pi) ? pi : null;
            var prodItemCode = prodItem?.ItemCode ?? string.Empty;
            var prodItemName = prodItem?.ItemName ?? string.Empty;
            var fgWhName = GetWarehouseName(wo.FgWarehouseId, warehouseMap);

            // FG available qty in FG warehouse
            var fgAvailable = AllotStock(wo.ItemId, wo.FgWarehouseId, wo.Quantity, binTracker);

            var items = wo.RequiredItems.ToList();
            if (!items.Any())
            {
                rows.Add(new ProductionPlanningReportRowDto
                {
                    RowId = Guid.NewGuid(),
                    OrderNo = wo.WorkOrderNumber,
                    OrderType = "Work Order",
                    ProductionItemCode = prodItemCode,
                    ProductionItemName = prodItemName,
                    ForWarehouse = fgWhName,
                    QtyToManufacture = wo.Quantity,
                    AvailableProductionQty = fgAvailable,
                    PlannedDate = wo.PlannedStartDate,
                    RequiredQty = 0,
                    AllottedQty = 0
                });
                continue;
            }

            foreach (var rm in items)
            {
                var rmWhId = input.RawMaterialWarehouseId ?? rm.SourceWarehouseId ?? wo.SourceWarehouseId ?? wo.FgWarehouseId;
                var rmWhName = GetWarehouseName(rmWhId, warehouseMap);

                var rawItem = itemMap.TryGetValue(rm.ItemId, out var ri) ? ri : null;
                var rawCode = rawItem?.ItemCode ?? string.Empty;
                var rawName = rawItem?.ItemName ?? rm.ItemName;

                var issuedQty = Math.Max(rm.TransferredQuantity, rm.ConsumedQuantity);
                var netRequiredQty = Math.Max(rm.RequiredQuantity - issuedQty, 0m);
                var allotted = AllotStock(rm.ItemId, rmWhId, netRequiredQty, binTracker);
                var (arrivalDate, arrivalQty) = GetPoArrival(openPOs, rm.ItemId, rmWhId);

                rows.Add(new ProductionPlanningReportRowDto
                {
                    RowId = Guid.NewGuid(),
                    OrderNo = wo.WorkOrderNumber,
                    OrderType = "Work Order",
                    ProductionItemCode = prodItemCode,
                    ProductionItemName = prodItemName,
                    ForWarehouse = fgWhName,
                    QtyToManufacture = wo.Quantity,
                    AvailableProductionQty = fgAvailable,
                    PlannedDate = wo.PlannedStartDate,
                    RawMaterialCode = rawCode,
                    RawMaterialName = rawName,
                    RawMaterialWarehouse = rmWhName,
                    RequiredQty = netRequiredQty,
                    AllottedQty = allotted,
                    ExpectedArrivalDate = arrivalDate,
                    ExpectedArrivalQty = arrivalQty
                });
            }
        }
    }

    private async Task ProcessSalesOrdersAsync(
        ProductionPlanningReportFilterDto input,
        Dictionary<Guid, Item> itemMap,
        Dictionary<Guid, Warehouse> warehouseMap,
        Dictionary<(Guid, Guid), decimal> binTracker,
        List<PurchaseOrder> openPOs,
        List<ProductionPlanningReportRowDto> rows)
    {
        var soQuery = await _salesOrderRepository.WithDetailsAsync(s => s.Items);
        var salesOrders = soQuery
            .Where(s => s.Status != DocumentStatus.Draft &&
                        s.Status != DocumentStatus.Cancelled &&
                        s.Status != DocumentStatus.Closed &&
                        s.Status != DocumentStatus.Completed &&
                        s.PerDelivered < 100m)
            .OrderBy(s => s.OrderDate)
            .ToList();

        if (input.CompanyId.HasValue)
        {
            salesOrders = salesOrders.Where(s => s.CompanyId == input.CompanyId.Value).ToList();
        }

        var boms = await _bomRepository.WithDetailsAsync(b => b.Items);
        var bomMap = boms.Where(b => b.IsActive).ToDictionary(b => b.ItemId);

        foreach (var so in salesOrders)
        {
            foreach (var soItem in so.Items)
            {
                var prodItem = itemMap.TryGetValue(soItem.ItemId, out var pi) ? pi : null;
                var prodItemCode = prodItem?.ItemCode ?? string.Empty;
                var prodItemName = prodItem?.ItemName ?? soItem.Description;

                var pendingQty = soItem.PendingDeliveryQty;
                if (pendingQty <= 0) continue;

                if (!bomMap.TryGetValue(soItem.ItemId, out var bom))
                {
                    continue;
                }

                var fgWhId = bom.TargetWarehouseId;
                var fgWhName = GetWarehouseName(fgWhId, warehouseMap);

                var fgAvailable = AllotStock(soItem.ItemId, fgWhId, pendingQty, binTracker);

                foreach (var bomItem in bom.Items)
                {
                    var reqQty = bom.Quantity > 0 ? (bomItem.Quantity / bom.Quantity) * pendingQty : bomItem.Quantity * pendingQty;

                    var rmWhId = input.RawMaterialWarehouseId ?? bomItem.SourceWarehouseId ?? bom.SourceWarehouseId ?? fgWhId;
                    var rmWhName = GetWarehouseName(rmWhId, warehouseMap);

                    var rawItem = itemMap.TryGetValue(bomItem.ItemId, out var ri) ? ri : null;
                    var rawCode = rawItem?.ItemCode ?? string.Empty;
                    var rawName = rawItem?.ItemName ?? bomItem.ItemName;

                    var allotted = AllotStock(bomItem.ItemId, rmWhId, reqQty, binTracker);
                    var (arrivalDate, arrivalQty) = GetPoArrival(openPOs, bomItem.ItemId, rmWhId);

                    rows.Add(new ProductionPlanningReportRowDto
                    {
                        RowId = Guid.NewGuid(),
                        OrderNo = so.OrderNumber,
                        OrderType = "Sales Order",
                        ProductionItemCode = prodItemCode,
                        ProductionItemName = prodItemName,
                        ForWarehouse = fgWhName,
                        QtyToManufacture = pendingQty,
                        AvailableProductionQty = fgAvailable,
                        PlannedDate = soItem.DeliveryDate ?? so.OrderDate,
                        RawMaterialCode = rawCode,
                        RawMaterialName = rawName,
                        RawMaterialWarehouse = rmWhName,
                        RequiredQty = reqQty,
                        AllottedQty = allotted,
                        ExpectedArrivalDate = arrivalDate,
                        ExpectedArrivalQty = arrivalQty
                    });
                }
            }
        }
    }

    private async Task ProcessMaterialRequestsAsync(
        ProductionPlanningReportFilterDto input,
        Dictionary<Guid, Item> itemMap,
        Dictionary<Guid, Warehouse> warehouseMap,
        Dictionary<(Guid, Guid), decimal> binTracker,
        List<PurchaseOrder> openPOs,
        List<ProductionPlanningReportRowDto> rows)
    {
        var mrQuery = await _materialRequestRepository.WithDetailsAsync(m => m.Items);
        var requests = mrQuery
            .Where(m => m.Status != DocumentStatus.Draft &&
                        m.Status != DocumentStatus.Cancelled &&
                        m.Status != DocumentStatus.Closed &&
                        m.Status != DocumentStatus.Completed &&
                        m.RequestType == MaterialRequestType.Manufacture &&
                        m.PerOrdered < 100m)
            .OrderBy(m => m.RequiredByDate ?? m.RequestDate)
            .ToList();

        if (input.CompanyId.HasValue)
        {
            requests = requests.Where(m => m.CompanyId == input.CompanyId.Value).ToList();
        }

        var boms = await _bomRepository.WithDetailsAsync(b => b.Items);
        var bomMap = boms.Where(b => b.IsActive).ToDictionary(b => b.ItemId);

        foreach (var mr in requests)
        {
            foreach (var mrItem in mr.Items)
            {
                var prodItem = itemMap.TryGetValue(mrItem.ItemId, out var pi) ? pi : null;
                var prodItemCode = prodItem?.ItemCode ?? string.Empty;
                var prodItemName = prodItem?.ItemName ?? mrItem.ItemName;

                var pendingQty = Math.Max(0m, mrItem.Quantity - mrItem.OrderedQuantity);
                if (pendingQty <= 0) continue;

                if (!bomMap.TryGetValue(mrItem.ItemId, out var bom))
                {
                    continue;
                }

                var fgWhId = mrItem.WarehouseId ?? mr.TargetWarehouseId ?? bom.TargetWarehouseId;
                var fgWhName = GetWarehouseName(fgWhId, warehouseMap);

                var fgAvailable = AllotStock(mrItem.ItemId, fgWhId, pendingQty, binTracker);

                foreach (var bomItem in bom.Items)
                {
                    var reqQty = bom.Quantity > 0 ? (bomItem.Quantity / bom.Quantity) * pendingQty : bomItem.Quantity * pendingQty;

                    var rmWhId = input.RawMaterialWarehouseId ?? bomItem.SourceWarehouseId ?? mr.SourceWarehouseId ?? bom.SourceWarehouseId;
                    var rmWhName = GetWarehouseName(rmWhId, warehouseMap);

                    var rawItem = itemMap.TryGetValue(bomItem.ItemId, out var ri) ? ri : null;
                    var rawCode = rawItem?.ItemCode ?? string.Empty;
                    var rawName = rawItem?.ItemName ?? bomItem.ItemName;

                    var allotted = AllotStock(bomItem.ItemId, rmWhId, reqQty, binTracker);
                    var (arrivalDate, arrivalQty) = GetPoArrival(openPOs, bomItem.ItemId, rmWhId);

                    rows.Add(new ProductionPlanningReportRowDto
                    {
                        RowId = Guid.NewGuid(),
                        OrderNo = mr.RequestNumber,
                        OrderType = "Material Request",
                        ProductionItemCode = prodItemCode,
                        ProductionItemName = prodItemName,
                        ForWarehouse = fgWhName,
                        QtyToManufacture = pendingQty,
                        AvailableProductionQty = fgAvailable,
                        PlannedDate = mr.RequiredByDate ?? mr.RequestDate,
                        RawMaterialCode = rawCode,
                        RawMaterialName = rawName,
                        RawMaterialWarehouse = rmWhName,
                        RequiredQty = reqQty,
                        AllottedQty = allotted,
                        ExpectedArrivalDate = arrivalDate,
                        ExpectedArrivalQty = arrivalQty
                    });
                }
            }
        }
    }

    private static string GetWarehouseName(Guid? warehouseId, Dictionary<Guid, Warehouse> warehouseMap)
    {
        if (warehouseId.HasValue && warehouseMap.TryGetValue(warehouseId.Value, out var wh))
        {
            return wh.Name;
        }
        return string.Empty;
    }

    private static decimal AllotStock(Guid itemId, Guid? warehouseId, decimal requiredQty, Dictionary<(Guid, Guid), decimal> binTracker)
    {
        if (!warehouseId.HasValue) return 0m;
        var key = (itemId, warehouseId.Value);
        if (binTracker.TryGetValue(key, out var available) && available > 0m)
        {
            var allotted = Math.Min(requiredQty, available);
            binTracker[key] = available - allotted;
            return allotted;
        }
        return 0m;
    }

    private static (DateTime? arrivalDate, decimal arrivalQty) GetPoArrival(
        List<PurchaseOrder> openPOs,
        Guid itemId,
        Guid? warehouseId)
    {
        var matchingItems = openPOs
            .SelectMany(po => po.Items.Select(item => new { PO = po, Item = item }))
            .Where(x => x.Item.ItemId == itemId && x.Item.PendingReceiptQty > 0)
            .Where(x => !warehouseId.HasValue || !x.Item.WarehouseId.HasValue || x.Item.WarehouseId.Value == warehouseId.Value)
            .ToList();

        if (!matchingItems.Any())
        {
            return (null, 0m);
        }

        var earliestDate = matchingItems
            .Select(x => x.Item.GetEffectiveExpectedDate(x.PO.ExpectedDeliveryDate))
            .Where(d => d.HasValue)
            .OrderBy(d => d)
            .FirstOrDefault();

        var totalQty = matchingItems.Sum(x => x.Item.PendingReceiptQty);

        return (earliestDate, totalQty);
    }
}
