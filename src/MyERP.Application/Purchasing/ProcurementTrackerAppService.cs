using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Projects.Entities;
using MyERP.Purchasing.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class ProcurementTrackerAppService : ApplicationService, IProcurementTrackerAppService
{
    private readonly IRepository<PurchaseOrder, Guid> _poRepo;
    private readonly IRepository<MaterialRequest, Guid> _mrRepo;
    private readonly IRepository<PurchaseReceipt, Guid> _prRepo;
    private readonly IRepository<PurchaseInvoice, Guid> _piRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<Warehouse, Guid> _warehouseRepo;
    private readonly IRepository<Project, Guid> _projectRepo;
    private readonly IRepository<CostCenter, Guid> _costCenterRepo;

    public ProcurementTrackerAppService(
        IRepository<PurchaseOrder, Guid> poRepo,
        IRepository<MaterialRequest, Guid> mrRepo,
        IRepository<PurchaseReceipt, Guid> prRepo,
        IRepository<PurchaseInvoice, Guid> piRepo,
        IRepository<Supplier, Guid> supplierRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<Warehouse, Guid> warehouseRepo,
        IRepository<Project, Guid> projectRepo,
        IRepository<CostCenter, Guid> costCenterRepo)
    {
        _poRepo = poRepo;
        _mrRepo = mrRepo;
        _prRepo = prRepo;
        _piRepo = piRepo;
        _supplierRepo = supplierRepo;
        _itemRepo = itemRepo;
        _warehouseRepo = warehouseRepo;
        _projectRepo = projectRepo;
        _costCenterRepo = costCenterRepo;
    }

    public async Task<ProcurementTrackerReportDto> GetReportAsync(ProcurementTrackerFilterDto input)
    {
        if (input.FromDate.HasValue && input.ToDate.HasValue && input.FromDate.Value > input.ToDate.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "To Date must be greater than or equal to From Date.");
        }

        // 1. Fetch Purchase Orders matching filters
        var poQuery = await _poRepo.WithDetailsAsync(p => p.Items);
        var pos = poQuery
            .Where(p => p.CompanyId == input.CompanyId
                     && p.Status != DocumentStatus.Draft
                     && p.Status != DocumentStatus.Cancelled)
            .WhereIf(!input.ShowCompletedOrders, p => p.Status != DocumentStatus.Closed && p.Status != DocumentStatus.Completed)
            .WhereIf(input.CostCenterId.HasValue, p => p.CostCenterId == input.CostCenterId!.Value)
            .WhereIf(input.ProjectId.HasValue, p => p.ProjectId == input.ProjectId!.Value || p.Items.Any(i => i.ProjectId == input.ProjectId!.Value))
            .WhereIf(input.FromDate.HasValue, p => p.OrderDate >= input.FromDate!.Value)
            .WhereIf(input.ToDate.HasValue, p => p.OrderDate <= input.ToDate!.Value)
            .ToList();

        var poItems = pos.SelectMany(p => p.Items.Select(i => new { Order = p, Item = i })).ToList();
        var poItemIds = poItems.Select(x => x.Item.Id).ToList();
        var requestItemIds = poItems
            .Where(x => x.Item.MaterialRequestItemId.HasValue)
            .Select(x => x.Item.MaterialRequestItemId!.Value)
            .Distinct()
            .ToList();

        // 2. Fetch Material Requests matching filters + linked requests
        var mrQuery = await _mrRepo.WithDetailsAsync(m => m.Items);
        var allMrs = mrQuery
            .Where(m => m.CompanyId == input.CompanyId
                     && m.Status == DocumentStatus.Submitted
                     && (m.RequestType == MaterialRequestType.Purchase || m.RequestType == MaterialRequestType.Subcontracting))
            .ToList();

        var mrItemMap = allMrs
            .SelectMany(m => m.Items.Select(i => new { Request = m, Item = i }))
            .ToDictionary(x => x.Item.Id, x => x);

        // Unordered MR items matching date & project filters
        var unorderedMrRows = allMrs
            .WhereIf(input.FromDate.HasValue, m => m.RequestDate >= input.FromDate!.Value)
            .WhereIf(input.ToDate.HasValue, m => m.RequestDate <= input.ToDate!.Value)
            .WhereIf(input.ProjectId.HasValue, m => m.ProjectId == input.ProjectId!.Value)
            .SelectMany(m => m.Items.Where(i => i.OrderedQuantity == 0)
                                    .WhereIf(input.ProjectId.HasValue, i => (i.ProjectId ?? m.ProjectId) == input.ProjectId!.Value)
                                    .Select(i => new { Request = m, Item = i }))
            .ToList();

        // 3. PR actual delivery dates
        var prDeliveryDates = new Dictionary<Guid, DateTime>();
        if (poItemIds.Count > 0)
        {
            var prQuery = await _prRepo.WithDetailsAsync(r => r.Items);
            var prs = prQuery
                .Where(r => r.CompanyId == input.CompanyId
                         && r.Status == DocumentStatus.Submitted
                         && !r.IsReturn)
                .ToList();

            prDeliveryDates = prs
                .SelectMany(r => r.Items.Where(i => i.PurchaseOrderItemId.HasValue)
                                        .Select(i => new { PoItemId = i.PurchaseOrderItemId!.Value, r.PostingDate }))
                .Where(x => poItemIds.Contains(x.PoItemId))
                .GroupBy(x => x.PoItemId)
                .ToDictionary(g => g.Key, g => g.Max(x => x.PostingDate));
        }

        // 4. PI actual costs
        var piInvoicedCosts = new Dictionary<Guid, decimal>();
        if (poItemIds.Count > 0)
        {
            var piQuery = await _piRepo.WithDetailsAsync(i => i.Items);
            var pis = piQuery
                .Where(i => i.CompanyId == input.CompanyId
                         && (i.Status == DocumentStatus.Submitted || i.Status == DocumentStatus.Posted)
                         && !i.IsReturn)
                .ToList();

            piInvoicedCosts = pis
                .SelectMany(i => i.Items.Where(item => item.PurchaseOrderItemId.HasValue)
                                        .Select(item => new { PoItemId = item.PurchaseOrderItemId!.Value, item.LineTotal }))
                .Where(x => poItemIds.Contains(x.PoItemId))
                .GroupBy(x => x.PoItemId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.LineTotal));
        }

        // 5. Total ordered qty per request item (for proportional estimated cost distribution)
        var totalOrderedQtyByRequestItem = poItems
            .Where(x => x.Item.MaterialRequestItemId.HasValue)
            .GroupBy(x => x.Item.MaterialRequestItemId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Item.Quantity));

        // 6. Lookups
        var supplierIds = pos.Select(p => p.SupplierId).Distinct().ToList();
        var suppliers = (await _supplierRepo.GetQueryableAsync())
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionary(s => s.Id, s => s.Name);

        var itemIds = poItems.Select(x => x.Item.ItemId)
            .Concat(unorderedMrRows.Select(x => x.Item.ItemId))
            .Distinct()
            .ToList();
        var items = (await _itemRepo.GetQueryableAsync())
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName, i.StandardBuyingPrice })
            .ToDictionary(i => i.Id, i => i);

        var warehouseIds = poItems.Where(x => x.Item.WarehouseId.HasValue).Select(x => x.Item.WarehouseId!.Value)
            .Concat(unorderedMrRows.Where(x => x.Item.WarehouseId.HasValue).Select(x => x.Item.WarehouseId!.Value))
            .Distinct()
            .ToList();
        var warehouses = (await _warehouseRepo.GetQueryableAsync())
            .Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name })
            .ToDictionary(w => w.Id, w => w.Name);

        var costCenterIds = pos.Where(p => p.CostCenterId.HasValue).Select(p => p.CostCenterId!.Value).Distinct().ToList();
        var costCenters = (await _costCenterRepo.GetQueryableAsync())
            .Where(c => costCenterIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionary(c => c.Id, c => c.Name);

        var projectIds = pos.Where(p => p.ProjectId.HasValue).Select(p => p.ProjectId!.Value)
            .Concat(poItems.Where(x => x.Item.ProjectId.HasValue).Select(x => x.Item.ProjectId!.Value))
            .Concat(unorderedMrRows.Where(x => x.Item.ProjectId.HasValue).Select(x => x.Item.ProjectId!.Value))
            .Concat(unorderedMrRows.Where(x => x.Request.ProjectId.HasValue).Select(x => x.Request.ProjectId!.Value))
            .Distinct()
            .ToList();
        var projects = (await _projectRepo.GetQueryableAsync())
            .Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, p.ProjectName })
            .ToDictionary(p => p.Id, p => p.ProjectName);

        var rows = new List<ProcurementTrackerRowDto>();

        // 7. Process unordered Material Requests
        foreach (var umr in unorderedMrRows)
        {
            var item = items.GetValueOrDefault(umr.Item.ItemId);
            var itemCode = item?.ItemCode ?? umr.Item.ItemName;
            var itemName = item?.ItemName ?? umr.Item.ItemName;
            var buyingRate = item?.StandardBuyingPrice ?? 0m;
            var estimatedCost = umr.Item.Quantity * buyingRate;
            var warehouseName = umr.Item.WarehouseId.HasValue ? warehouses.GetValueOrDefault(umr.Item.WarehouseId.Value) : null;
            var projId = umr.Item.ProjectId ?? umr.Request.ProjectId;
            var projectName = projId.HasValue ? projects.GetValueOrDefault(projId.Value) : null;

            rows.Add(new ProcurementTrackerRowDto
            {
                MaterialRequestDate = umr.Request.RequestDate,
                MaterialRequestNo = umr.Request.RequestNumber,
                CostCenter = null,
                Project = projectName,
                RequestingSite = warehouseName,
                Requestor = umr.Request.CreatorId?.ToString() ?? "System",
                ItemCode = itemCode,
                ItemName = itemName,
                Quantity = umr.Item.Quantity,
                UnitOfMeasurement = umr.Item.Uom,
                Status = umr.Request.Status.ToString(),
                PurchaseOrderDate = null,
                PurchaseOrderNo = null,
                SupplierName = null,
                EstimatedCost = estimatedCost,
                ActualCost = 0m,
                PurchaseOrderAmount = 0m,
                PurchaseOrderAmountCompanyCurrency = 0m,
                ExpectedDeliveryDate = null,
                ActualDeliveryDate = null
            });
        }

        // 8. Process Purchase Orders
        foreach (var poEntry in poItems)
        {
            var po = poEntry.Order;
            var poItem = poEntry.Item;

            var item = items.GetValueOrDefault(poItem.ItemId);
            var itemCode = item?.ItemCode ?? poItem.Description;
            var itemName = item?.ItemName ?? poItem.Description;

            var supplierName = suppliers.GetValueOrDefault(po.SupplierId, "Unknown Supplier");
            var costCenterName = po.CostCenterId.HasValue ? costCenters.GetValueOrDefault(po.CostCenterId.Value) : null;
            var projId = poItem.ProjectId ?? po.ProjectId;
            var projectName = projId.HasValue ? projects.GetValueOrDefault(projId.Value) : null;
            var warehouseName = poItem.WarehouseId.HasValue ? warehouses.GetValueOrDefault(poItem.WarehouseId.Value) : null;

            // Link MR
            DateTime? mrDate = null;
            string? mrNo = null;
            string? requestor = po.CreatorId?.ToString() ?? "System";
            decimal estimatedCost = poItem.LineTotal;

            if (poItem.MaterialRequestItemId.HasValue && mrItemMap.TryGetValue(poItem.MaterialRequestItemId.Value, out var mrEntry))
            {
                mrDate = mrEntry.Request.RequestDate;
                mrNo = mrEntry.Request.RequestNumber;
                requestor = mrEntry.Request.CreatorId?.ToString() ?? requestor;

                var mrBuyingRate = item?.StandardBuyingPrice ?? poItem.UnitPrice;
                var totalMrAmount = mrEntry.Item.Quantity * mrBuyingRate;
                var totalOrdered = totalOrderedQtyByRequestItem.GetValueOrDefault(poItem.MaterialRequestItemId.Value, poItem.Quantity);

                estimatedCost = totalOrdered > 0 ? (totalMrAmount * (poItem.Quantity / totalOrdered)) : totalMrAmount;
            }

            // Actual Cost: Invoiced amount, or PO line amount while order can still be billed (0 if Closed)
            decimal actualCost;
            if (piInvoicedCosts.TryGetValue(poItem.Id, out var invoiced))
            {
                actualCost = invoiced;
            }
            else
            {
                actualCost = po.Status == DocumentStatus.Closed ? 0m : poItem.LineTotal;
            }

            var actualDeliveryDate = prDeliveryDates.GetValueOrDefault(poItem.Id);
            var expectedDeliveryDate = poItem.ExpectedDeliveryDate ?? po.ExpectedDeliveryDate;

            rows.Add(new ProcurementTrackerRowDto
            {
                MaterialRequestDate = mrDate,
                MaterialRequestNo = mrNo,
                CostCenter = costCenterName,
                Project = projectName,
                RequestingSite = warehouseName,
                Requestor = requestor,
                ItemCode = itemCode,
                ItemName = itemName,
                Quantity = poItem.Quantity,
                UnitOfMeasurement = poItem.Uom,
                Status = po.Status.ToString(),
                PurchaseOrderDate = po.OrderDate,
                PurchaseOrderNo = po.OrderNumber,
                SupplierName = supplierName,
                EstimatedCost = estimatedCost,
                ActualCost = actualCost,
                PurchaseOrderAmount = poItem.LineTotal,
                PurchaseOrderAmountCompanyCurrency = poItem.LineTotal,
                ExpectedDeliveryDate = expectedDeliveryDate,
                ActualDeliveryDate = actualDeliveryDate == default ? null : actualDeliveryDate
            });
        }

        var distinctMrs = rows.Where(r => !string.IsNullOrEmpty(r.MaterialRequestNo)).Select(r => r.MaterialRequestNo).Distinct().Count();
        var distinctPos = rows.Where(r => !string.IsNullOrEmpty(r.PurchaseOrderNo)).Select(r => r.PurchaseOrderNo).Distinct().Count();

        return new ProcurementTrackerReportDto
        {
            Rows = rows,
            Summary = new ProcurementTrackerSummaryDto
            {
                TotalRequisitions = distinctMrs,
                TotalOrders = distinctPos,
                TotalEstimatedCost = rows.Sum(r => r.EstimatedCost),
                TotalActualCost = rows.Sum(r => r.ActualCost),
                TotalPurchaseOrderAmount = rows.Sum(r => r.PurchaseOrderAmount)
            }
        };
    }
}
