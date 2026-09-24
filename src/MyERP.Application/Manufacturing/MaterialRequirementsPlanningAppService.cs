using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

/// <summary>
/// Material Requirements Planning (MRP) Report App Service.
/// Implements time-bucketed planning across MPS, Sales Orders, BOM explosions, and scheduled receipts.
/// Maps to ERPNext manufacturing/report/material_requirements_planning_report.
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class MaterialRequirementsPlanningAppService : ApplicationService, IMaterialRequirementsPlanningAppService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<MasterProductionSchedule, Guid> _mpsRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly MasterProductionScheduleService _leadTimeService;

    public MaterialRequirementsPlanningAppService(
        IRepository<Item, Guid> itemRepository,
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<MasterProductionSchedule, Guid> mpsRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<PurchaseOrder, Guid> purchaseOrderRepository,
        IRepository<Bin, Guid> binRepository,
        IDocumentNumberGenerator numberGenerator,
        MasterProductionScheduleService leadTimeService)
    {
        _itemRepository = itemRepository;
        _bomRepository = bomRepository;
        _mpsRepository = mpsRepository;
        _salesOrderRepository = salesOrderRepository;
        _workOrderRepository = workOrderRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _binRepository = binRepository;
        _numberGenerator = numberGenerator;
        _leadTimeService = leadTimeService;
    }

    /// <summary>
    /// Generates time buckets according to bucket size and date range.
    /// Per ERPNext PR #59143 (commit 5d5eeb5e02):
    /// Uses 'while (from_date <= to_date)' so that a to_date landing on a bucket boundary
    /// is included in the period columns.
    /// </summary>
    public static List<MrpPeriodBucketDto> GenerateBuckets(DateTime fromDate, DateTime toDate, MrpBucketSize bucketSize)
    {
        var from = fromDate.Date;
        if (bucketSize == MrpBucketSize.Weekly)
        {
            // First date of week (Monday)
            int diff = (7 + (from.DayOfWeek - DayOfWeek.Monday)) % 7;
            from = from.AddDays(-1 * diff).Date;
        }
        else if (bucketSize == MrpBucketSize.Monthly)
        {
            from = new DateTime(from.Year, from.Month, 1);
        }

        var buckets = new List<MrpPeriodBucketDto>();

        // PR #59143: include the mrp bucket that ends on to_date (from <= toDate)
        while (from <= toDate.Date)
        {
            var bucketFrom = from;
            DateTime nextDate;
            if (bucketSize == MrpBucketSize.Monthly)
                nextDate = from.AddMonths(1);
            else if (bucketSize == MrpBucketSize.Weekly)
                nextDate = from.AddDays(7);
            else
                nextDate = from.AddDays(1);

            var bucketTo = nextDate.AddDays(-1);
            var label = bucketSize switch
            {
                MrpBucketSize.Monthly => bucketFrom.ToString("MMM yyyy"),
                MrpBucketSize.Weekly => $"{bucketFrom:dd MMM} - {bucketTo:dd MMM}",
                _ => bucketFrom.ToString("dd-MMM-yyyy")
            };

            buckets.Add(new MrpPeriodBucketDto
            {
                FromDate = bucketFrom,
                ToDate = bucketTo,
                Label = label
            });

            from = nextDate;
        }

        return buckets;
    }

    public async Task<MaterialRequirementsPlanningReportDto> GetReportAsync(MaterialRequirementsPlanningFilterDto input)
    {
        var from = input.FromDate != default ? input.FromDate.Date : DateTime.UtcNow.Date;
        var to = input.ToDate != default ? input.ToDate.Date : from.AddMonths(3);

        if (to < from)
        {
            to = from;
        }

        var buckets = GenerateBuckets(from, to, input.BucketSize);
        if (buckets.Count == 0)
        {
            return new MaterialRequirementsPlanningReportDto();
        }

        var minDate = buckets.First().FromDate;
        var maxDate = buckets.Last().ToDate;

        // 1. Fetch relevant items
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var allItems = itemQuery
            .Where(i => i.IsActive && i.MaintainStock)
            .WhereIf(input.ItemId.HasValue, i => i.Id == input.ItemId!.Value)
            .ToList();

        if (allItems.Count == 0)
        {
            return new MaterialRequirementsPlanningReportDto { Buckets = buckets };
        }

        var itemMap = allItems.ToDictionary(i => i.Id);
        var itemIds = itemMap.Keys.ToHashSet();

        // 2. Fetch current stock balances from Bin
        var binQuery = await _binRepository.GetQueryableAsync();
        var bins = binQuery
            .Where(b => itemIds.Contains(b.ItemId))
            .WhereIf(input.WarehouseId.HasValue, b => b.WarehouseId == input.WarehouseId!.Value)
            .ToList();
        var stockMap = bins
            .GroupBy(b => b.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(b => b.ActualQty));

        // 3. Active BOMs for explosion
        var bomQuery = await _bomRepository.GetQueryableAsync();
        var activeBoms = bomQuery
            .Where(b => b.CompanyId == input.CompanyId && b.IsActive)
            .ToList();
        var itemBomMap = activeBoms
            .GroupBy(b => b.ItemId)
            .ToDictionary(g => g.Key, g => g.FirstOrDefault(b => b.IsDefault) ?? g.First());

        // 4. Gross Requirements: Demand
        // a. Open Sales Orders
        var soQuery = await _salesOrderRepository.GetQueryableAsync();
        var salesOrders = soQuery
            .Where(s => s.CompanyId == input.CompanyId
                     && s.Status != DocumentStatus.Draft
                     && s.Status != DocumentStatus.Cancelled
                     && s.Status != DocumentStatus.Completed)
            .ToList();

        var demandList = new List<(Guid ItemId, DateTime Date, decimal Qty)>();

        foreach (var so in salesOrders)
        {
            foreach (var line in so.Items.Where(i => itemIds.Contains(i.ItemId)))
            {
                var remaining = line.Quantity - line.DeliveredQty;
                if (remaining > 0)
                {
                    var reqDate = (line.DeliveryDate ?? so.OrderDate).Date;
                    if (reqDate >= minDate && reqDate <= maxDate)
                    {
                        demandList.Add((line.ItemId, reqDate, remaining));
                    }
                }
            }
        }

        // b. Master Production Schedule demand
        var mpsQuery = await _mpsRepository.GetQueryableAsync();
        var mpsList = mpsQuery
            .Where(m => m.CompanyId == input.CompanyId
                     && (m.Status == DocumentStatus.Submitted || m.Status == DocumentStatus.Draft))
            .ToList();

        foreach (var mps in mpsList)
        {
            foreach (var item in mps.Items.Where(i => itemIds.Contains(i.ItemId)))
            {
                var reqDate = item.DeliveryDate.Date;
                if (reqDate >= minDate && reqDate <= maxDate && item.PlannedQty > 0)
                {
                    demandList.Add((item.ItemId, reqDate, item.PlannedQty));
                }
            }
        }

        // c. Explode BOM requirements for raw materials
        var explodedDemand = new List<(Guid ItemId, DateTime Date, decimal Qty)>();
        foreach (var d in demandList)
        {
            if (itemBomMap.TryGetValue(d.ItemId, out var bom))
            {
                var baseQty = bom.Quantity > 0 ? bom.Quantity : 1m;
                var scale = d.Qty / baseQty;
                foreach (var bomItem in bom.Items)
                {
                    if (itemIds.Contains(bomItem.ItemId))
                    {
                        explodedDemand.Add((bomItem.ItemId, d.Date, bomItem.Quantity * scale));
                    }
                }
            }
        }
        demandList.AddRange(explodedDemand);

        // 5. Scheduled Receipts: Supply
        // a. Open Work Orders
        var woQuery = await _workOrderRepository.GetQueryableAsync();
        var workOrders = woQuery
            .Where(w => w.CompanyId == input.CompanyId
                     && w.Status != WorkOrderStatus.Completed
                     && w.Status != WorkOrderStatus.Cancelled
                     && w.Status != WorkOrderStatus.Draft)
            .ToList();

        var receiptList = new List<(Guid ItemId, DateTime Date, decimal Qty)>();

        foreach (var wo in workOrders.Where(w => itemIds.Contains(w.ItemId)))
        {
            var openQty = wo.Quantity - wo.ProducedQuantity;
            if (openQty > 0)
            {
                var receiptDate = (wo.PlannedEndDate ?? wo.PlannedStartDate ?? DateTime.UtcNow).Date;
                if (receiptDate >= minDate && receiptDate <= maxDate)
                {
                    receiptList.Add((wo.ItemId, receiptDate, openQty));
                }
            }
        }

        // b. Open Purchase Orders
        var poQuery = await _purchaseOrderRepository.GetQueryableAsync();
        var purchaseOrders = poQuery
            .Where(p => p.CompanyId == input.CompanyId
                     && p.Status == DocumentStatus.Submitted)
            .ToList();

        foreach (var po in purchaseOrders)
        {
            foreach (var line in po.Items.Where(i => itemIds.Contains(i.ItemId)))
            {
                var openQty = line.Quantity - line.ReceivedQty;
                if (openQty > 0)
                {
                    var receiptDate = (line.ExpectedDeliveryDate ?? po.OrderDate).Date;
                    if (receiptDate >= minDate && receiptDate <= maxDate)
                    {
                        receiptList.Add((line.ItemId, receiptDate, openQty));
                    }
                }
            }
        }

        // 6. Build MRP Rows per Item
        var rows = new List<MrpItemRowDto>();

        foreach (var item in allItems)
        {
            var currentStock = stockMap.GetValueOrDefault(item.Id, 0m);
            var isRm = !itemBomMap.ContainsKey(item.Id);
            var safetyStock = input.IncludeSafetyStock ? item.SafetyStock : 0m;

            var itemRow = new MrpItemRowDto
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                Uom = item.Uom,
                IsRawMaterial = isRm,
                SafetyStock = safetyStock,
                CurrentStock = currentStock,
                Buckets = new()
            };

            var runningBalance = currentStock;

            foreach (var bucket in buckets)
            {
                var grossReq = demandList
                    .Where(d => d.ItemId == item.Id && d.Date >= bucket.FromDate && d.Date <= bucket.ToDate)
                    .Sum(d => d.Qty);

                var scheduledRec = receiptList
                    .Where(r => r.ItemId == item.Id && r.Date >= bucket.FromDate && r.Date <= bucket.ToDate)
                    .Sum(r => r.Qty);

                var projectedBalance = runningBalance + scheduledRec - grossReq;
                var plannedOrders = 0m;

                if (projectedBalance < safetyStock)
                {
                    var shortage = safetyStock - projectedBalance;
                    plannedOrders = Math.Max(0m, shortage);
                    projectedBalance += plannedOrders;
                }

                runningBalance = projectedBalance;

                itemRow.Buckets.Add(new MrpItemBucketDataDto
                {
                    BucketFromDate = bucket.FromDate,
                    GrossRequirements = Math.Round(grossReq, 4),
                    ScheduledReceipts = Math.Round(scheduledRec, 4),
                    ProjectedAvailableBalance = Math.Round(projectedBalance, 4),
                    PlannedOrders = Math.Round(plannedOrders, 4)
                });
            }

            // Include if item has stock, demand, supply, or planned orders
            var hasActivity = currentStock > 0
                || itemRow.Buckets.Any(b => b.GrossRequirements > 0 || b.ScheduledReceipts > 0 || b.PlannedOrders > 0);

            if (hasActivity || input.ItemId.HasValue)
            {
                rows.Add(itemRow);
            }
        }

        // 7. Build Planned Order Requirements (PR #58510 & PR #59007)
        var requirements = new List<MrpPlannedOrderRequirementDto>();
        var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Supplier, Guid>>();
        var suppliers = (await supplierRepo.GetQueryableAsync())
            .Where(s => s.CompanyId == input.CompanyId && s.IsActive)
            .ToList();
        var supplierMap = suppliers.ToDictionary(s => s.Id);

        var itemLeadTimeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ItemLeadTime, Guid>>();
        var itemLeadTimes = (await itemLeadTimeRepo.WithDetailsAsync(lt => lt.Suppliers)).ToList();
        var itemLeadTimeMap = itemLeadTimes.ToDictionary(lt => lt.ItemId);

        var itemDefaultRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ItemDefault, Guid>>();
        var itemDefaults = (await itemDefaultRepo.GetQueryableAsync())
            .Where(d => d.CompanyId == input.CompanyId && itemIds.Contains(d.ItemId))
            .ToList();
        var itemDefaultMap = itemDefaults
            .Where(d => d.DefaultSupplierId.HasValue)
            .ToDictionary(d => d.ItemId, d => d.DefaultSupplierId!.Value);

        var itemGroupRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ItemGroup, Guid>>();
        var itemGroups = await itemGroupRepo.GetListAsync();
        var itemGroupMap = itemGroups.ToDictionary(g => g.Id);

        foreach (var itemRow in rows)
        {
            var item = itemMap[itemRow.ItemId];
            var hasBom = itemBomMap.TryGetValue(item.Id, out var bom);

            Guid? defaultSupplierId = null;
            string? defaultSupplierName = null;
            if (itemLeadTimeMap.TryGetValue(item.Id, out var itemLeadTime))
            {
                var defaultSup = itemLeadTime.Suppliers.FirstOrDefault(s => s.IsDefault)
                    ?? itemLeadTime.Suppliers.FirstOrDefault();
                if (defaultSup != null)
                {
                    defaultSupplierId = defaultSup.SupplierId;
                }
            }

            if (!defaultSupplierId.HasValue && itemDefaultMap.TryGetValue(item.Id, out var idSupId))
            {
                defaultSupplierId = idSupId;
            }

            if (!defaultSupplierId.HasValue && item.ItemGroupId.HasValue)
            {
                var curGroupId = (Guid?)item.ItemGroupId.Value;
                int maxDepth = 10;
                while (curGroupId.HasValue && maxDepth-- > 0)
                {
                    if (itemGroupMap.TryGetValue(curGroupId.Value, out var grp))
                    {
                        if (grp.DefaultSupplierId.HasValue)
                        {
                            defaultSupplierId = grp.DefaultSupplierId.Value;
                            break;
                        }
                        curGroupId = grp.ParentId;
                    }
                    else break;
                }
            }

            if (defaultSupplierId.HasValue)
            {
                defaultSupplierName = supplierMap.GetValueOrDefault(defaultSupplierId.Value)?.Name;
            }

            foreach (var b in itemRow.Buckets)
            {
                if (b.PlannedOrders > 0)
                {
                    var leadTimeDays = await _leadTimeService.GetCumulativeLeadTimeDaysAsync(item.Id, bom?.Id, b.PlannedOrders);
                    var deliveryDate = b.BucketFromDate;
                    var releaseDate = deliveryDate.AddDays(-leadTimeDays);

                    requirements.Add(new MrpPlannedOrderRequirementDto
                    {
                        ItemId = item.Id,
                        ItemCode = item.ItemCode,
                        ItemName = item.ItemName,
                        Uom = item.Uom,
                        TypeOfMaterial = hasBom ? "Manufacture" : "Purchase",
                        BomId = bom?.Id,
                        BomNo = bom?.BomNumber,
                        RequiredQty = b.PlannedOrders,
                        PlannedQty = b.PlannedOrders,
                        ProjectedQty = b.ProjectedAvailableBalance,
                        SafetyStock = itemRow.SafetyStock,
                        LeadTimeDays = leadTimeDays,
                        DeliveryDate = deliveryDate,
                        ReleaseDate = releaseDate,
                        DefaultSupplierId = defaultSupplierId,
                        DefaultSupplierName = defaultSupplierName,
                        WarehouseId = input.WarehouseId
                    });
                }
            }
        }

        return new MaterialRequirementsPlanningReportDto
        {
            Buckets = buckets,
            Rows = rows.OrderBy(r => r.IsRawMaterial).ThenBy(r => r.ItemCode).ToList(),
            Requirements = requirements.OrderBy(r => r.ReleaseDate).ThenBy(r => r.ItemCode).ToList()
        };
    }

    /// <summary>
    /// Creates draft Purchase Orders and Work Orders directly from MRP planned order rows (PR #58510 & PR #58511).
    /// Grouped by supplier/release date for purchasing, and guarded with mandatory BOM check for manufacturing.
    /// </summary>
    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<MrpOrdersCreatedDto> CreateOrdersAsync(CreateOrdersFromMrpInput input)
    {
        if (input.SelectedRows == null || input.SelectedRows.Count == 0)
        {
            return new MrpOrdersCreatedDto { Message = "No rows selected." };
        }

        var purchaseRows = input.SelectedRows
            .Where(r => string.Equals(r.TypeOfMaterial, "Purchase", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var manufactureRows = input.SelectedRows
            .Where(r => string.Equals(r.TypeOfMaterial, "Manufacture", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var bomQuery = await _bomRepository.WithDetailsAsync(b => b.Items);
        var activeBoms = bomQuery
            .Where(b => b.CompanyId == input.CompanyId && b.IsActive)
            .ToList();

        // PR #58510 & #58511: prompt/throw when manufactured item has no BOM
        var missingBoms = new List<string>();
        foreach (var row in manufactureRows)
        {
            if (!row.BomId.HasValue || row.BomId.Value == Guid.Empty)
            {
                var defaultBom = activeBoms.FirstOrDefault(b => b.ItemId == row.ItemId && b.IsDefault)
                    ?? activeBoms.FirstOrDefault(b => b.ItemId == row.ItemId);
                if (defaultBom != null)
                {
                    row.BomId = defaultBom.Id;
                }
                else
                {
                    missingBoms.Add(row.ItemCode);
                }
            }
        }

        if (missingBoms.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Default BOM for {string.Join(", ", missingBoms.Distinct())} not found");
        }

        var createdWorkOrderIds = new List<Guid>();
        foreach (var row in manufactureRows)
        {
            var woNumber = await _numberGenerator.GenerateAsync("WO", input.CompanyId);
            var plannedStart = row.ReleaseDate ?? row.DeliveryDate;
            var plannedEnd = row.DeliveryDate;
            if (plannedStart > plannedEnd) plannedStart = plannedEnd;

            var wo = new WorkOrder(
                GuidGenerator.Create(),
                input.CompanyId,
                woNumber,
                row.ItemId,
                row.BomId!.Value,
                row.Quantity,
                CurrentTenant.Id)
            {
                FgWarehouseId = row.WarehouseId ?? input.WarehouseId,
                Notes = input.MpsId.HasValue ? $"Created from MRP / MPS {input.MpsId.Value}" : "Created from MRP Report"
            };
            wo.SetPlannedDates(plannedStart, plannedEnd);

            var bom = activeBoms.FirstOrDefault(b => b.Id == row.BomId.Value);
            if (bom != null)
            {
                var multiplier = bom.Quantity > 0 ? row.Quantity / bom.Quantity : row.Quantity;
                foreach (var bi in bom.Items)
                {
                    var rawWarehouse = bi.SourceWarehouseId ?? row.WarehouseId ?? input.WarehouseId;
                    var itemEntity = await _itemRepository.FindAsync(bi.ItemId);
                    var itemName = itemEntity?.ItemName ?? bi.ItemName ?? "Item";
                    var woItem = new WorkOrderItem(
                        GuidGenerator.Create(),
                        wo.Id,
                        bi.ItemId,
                        itemName,
                        bi.Quantity * multiplier)
                    {
                        SourceWarehouseId = rawWarehouse
                    };
                    wo.RequiredItems.Add(woItem);
                }
            }

            await _workOrderRepository.InsertAsync(wo);
            createdWorkOrderIds.Add(wo.Id);
        }

        var createdPurchaseOrderIds = new List<Guid>();
        if (purchaseRows.Count > 0)
        {
            var itemIds = purchaseRows.Select(r => r.ItemId).Distinct().ToList();
            var items = (await _itemRepository.GetQueryableAsync())
                .Where(i => itemIds.Contains(i.Id))
                .ToList();
            var itemMap = items.ToDictionary(i => i.Id);

            var itemDefaultRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ItemDefault, Guid>>();
            var itemDefaults = (await itemDefaultRepo.GetQueryableAsync())
                .Where(d => d.CompanyId == input.CompanyId && itemIds.Contains(d.ItemId))
                .ToList();
            var itemDefaultMap = itemDefaults
                .Where(d => d.DefaultSupplierId.HasValue)
                .ToDictionary(d => d.ItemId, d => d.DefaultSupplierId!.Value);

            var itemGroupRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ItemGroup, Guid>>();
            var itemGroups = await itemGroupRepo.GetListAsync();
            var itemGroupMap = itemGroups.ToDictionary(g => g.Id);

            // PR #59349: Resolve supplier per item with ItemGroup fallback; throw if missing
            var missingSuppliers = new List<string>();
            foreach (var row in purchaseRows)
            {
                if (!row.DefaultSupplierId.HasValue || row.DefaultSupplierId.Value == Guid.Empty)
                {
                    if (itemDefaultMap.TryGetValue(row.ItemId, out var supId))
                    {
                        row.DefaultSupplierId = supId;
                    }
                    else if (itemMap.TryGetValue(row.ItemId, out var item) && item.ItemGroupId.HasValue)
                    {
                        var curGroupId = (Guid?)item.ItemGroupId.Value;
                        int maxDepth = 10;
                        while (curGroupId.HasValue && maxDepth-- > 0)
                        {
                            if (itemGroupMap.TryGetValue(curGroupId.Value, out var grp))
                            {
                                if (grp.DefaultSupplierId.HasValue)
                                {
                                    row.DefaultSupplierId = grp.DefaultSupplierId.Value;
                                    break;
                                }
                                curGroupId = grp.ParentId;
                            }
                            else break;
                        }
                    }

                    if (!row.DefaultSupplierId.HasValue || row.DefaultSupplierId.Value == Guid.Empty)
                    {
                        missingSuppliers.Add(row.ItemCode);
                    }
                }
            }

            if (missingSuppliers.Count > 0)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Default Supplier for {string.Join(", ", missingSuppliers.Distinct())} not found");
            }

            var groupedPurchases = purchaseRows.GroupBy(r =>
            {
                var releaseDate = (r.ReleaseDate ?? r.DeliveryDate).Date;
                return (SupplierId: r.DefaultSupplierId!.Value, ReleaseDate: releaseDate);
            });

            foreach (var grp in groupedPurchases)
            {

                var poNumber = await _numberGenerator.GenerateAsync("PurchaseOrder", input.CompanyId);
                var po = new PurchaseOrder(
                    GuidGenerator.Create(),
                    input.CompanyId,
                    grp.Key.SupplierId,
                    poNumber,
                    grp.Key.ReleaseDate)
                {
                    ExpectedDeliveryDate = grp.Max(r => r.DeliveryDate)
                };

                foreach (var row in grp)
                {
                    var itemEntity = itemMap.GetValueOrDefault(row.ItemId);
                    var uom = itemEntity?.Uom ?? "Nos";
                    var warehouse = row.WarehouseId ?? input.WarehouseId;
                    po.AddItem(
                        row.ItemId,
                        row.ItemName ?? itemEntity?.ItemName ?? row.ItemCode,
                        row.Quantity,
                        itemEntity?.StandardBuyingPrice ?? 0m,
                        0m,
                        uom,
                        warehouse);
                }

                await _purchaseOrderRepository.InsertAsync(po);
                createdPurchaseOrderIds.Add(po.Id);
            }
        }

        return new MrpOrdersCreatedDto
        {
            PurchaseOrderIds = createdPurchaseOrderIds,
            WorkOrderIds = createdWorkOrderIds,
            Message = $"Created {createdPurchaseOrderIds.Count} Purchase Order(s) and {createdWorkOrderIds.Count} Work Order(s)."
        };
    }
}
