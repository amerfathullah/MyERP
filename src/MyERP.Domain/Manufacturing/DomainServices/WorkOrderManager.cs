using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Manufacturing.DomainServices;

/// <summary>
/// Domain service for Work Order business rules.
/// Validates production items, manages raw material consumption,
/// enforces overproduction limits, and handles WO lifecycle side effects.
/// </summary>
public class WorkOrderManager : DomainService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<ManufacturingSettings, Guid> _settingsRepository;

    public WorkOrderManager(
        IRepository<Item, Guid> itemRepository,
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<ManufacturingSettings, Guid> settingsRepository)
    {
        _itemRepository = itemRepository;
        _bomRepository = bomRepository;
        _settingsRepository = settingsRepository;
    }

    /// <summary>
    /// Validates the production item is eligible for manufacturing.
    /// Per ERPNext: template items, end-of-life items, and non-producible items are blocked.
    /// </summary>
    public async Task ValidateProductionItemAsync(Guid itemId)
    {
        var item = await _itemRepository.GetAsync(itemId);

        if (item.HasVariants)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ItemHasVariants)
                .WithData("itemCode", item.ItemCode);
        }

        if (!item.IsActive)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ItemInactive)
                .WithData("itemCode", item.ItemCode)
                .WithData("itemName", item.ItemName);
        }
    }

    /// <summary>
    /// Validates the BOM is active and matches the production item.
    /// </summary>
    public async Task ValidateBomAsync(Guid bomId, Guid itemId)
    {
        var bom = await _bomRepository.GetAsync(bomId);

        if (!bom.IsActive)
        {
            throw new BusinessException("MyERP:10010")
                .WithData("bomId", bomId);
        }

        if (bom.ItemId != itemId)
        {
            var item = await _itemRepository.FindAsync(itemId);
            if (item?.VariantOfId == null || bom.ItemId != item.VariantOfId.Value)
            {
                throw new BusinessException("MyERP:10011")
                    .WithData("bomItem", bom.ItemId)
                    .WithData("woItem", itemId);
            }
        }
    }

    /// <summary>
    /// Calculates proportional raw material quantities for a given production quantity.
    /// bomItem.Quantity × (produceQty / bom.Quantity)
    /// Per ERPNext PR #58231: falls back to Item default warehouse, then ItemGroup default warehouse.
    /// Per ERPNext PR #59445 (commit fd8e6230f3): explodes phantom BOM rows recursively by their stock qty.
    /// </summary>
    public async Task<WorkOrderMaterialRequirement[]> CalculateMaterialRequirementsAsync(
        Guid bomId, decimal produceQty, IRepository<ItemGroup, Guid>? itemGroupRepository = null)
    {
        var collected = new System.Collections.Generic.List<WorkOrderMaterialRequirement>();
        var visitedBoms = new System.Collections.Generic.HashSet<Guid>();
        await ExplodeMaterialRequirementsInternalAsync(bomId, produceQty, collected, itemGroupRepository, visitedBoms);

        // Aggregate identical items at the same source warehouse
        return collected
            .GroupBy(c => new { c.ItemId, c.SourceWarehouseId })
            .Select(g =>
            {
                var totalQty = g.Sum(x => x.RequiredQty);
                var totalCost = g.Sum(x => x.RequiredQty * x.Rate);
                var rate = totalQty > 0 ? totalCost / totalQty : g.First().Rate;
                return new WorkOrderMaterialRequirement
                {
                    ItemId = g.Key.ItemId,
                    ItemName = g.First().ItemName,
                    RequiredQty = Math.Round(totalQty, 4),
                    Rate = rate,
                    SourceWarehouseId = g.Key.SourceWarehouseId
                };
            })
            .ToArray();
    }

    private async Task ExplodeMaterialRequirementsInternalAsync(
        Guid bomId,
        decimal produceQty,
        System.Collections.Generic.List<WorkOrderMaterialRequirement> collected,
        IRepository<ItemGroup, Guid>? itemGroupRepository,
        System.Collections.Generic.HashSet<Guid> visitedBoms)
    {
        if (visitedBoms.Contains(bomId)) return;
        visitedBoms.Add(bomId);

        var bom = await _bomRepository.GetAsync(bomId);
        var itemIds = bom.Items.Select(i => i.ItemId).Distinct().ToList();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var itemMap = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionary(i => i.Id, i => i);

        var itemGroupMap = new System.Collections.Generic.Dictionary<Guid, ItemGroup>();
        if (itemGroupRepository != null)
        {
            var groupIds = itemMap.Values.Where(i => i.ItemGroupId.HasValue).Select(i => i.ItemGroupId!.Value).Distinct().ToList();
            if (groupIds.Any())
            {
                var groupQuery = await itemGroupRepository.GetQueryableAsync();
                itemGroupMap = groupQuery.Where(g => groupIds.Contains(g.Id)).ToDictionary(g => g.Id, g => g);
            }
        }

        var bomOutputQty = bom.Quantity > 0 ? bom.Quantity : 1m;

        foreach (var i in bom.Items)
        {
            if (i.IsPhantom && i.SubBomId.HasValue && !i.DoNotExplode)
            {
                // Explode phantom sub-BOM by stock qty per ERPNext PR #59445
                var subProduceQty = (i.StockQty / bomOutputQty) * produceQty;
                await ExplodeMaterialRequirementsInternalAsync(i.SubBomId.Value, subProduceQty, collected, itemGroupRepository, visitedBoms);
            }
            else if (!i.IsPhantom)
            {
                itemMap.TryGetValue(i.ItemId, out var item);
                ItemGroup? group = null;
                if (item?.ItemGroupId != null)
                    itemGroupMap.TryGetValue(item.ItemGroupId.Value, out group);

                var sourceWarehouseId = i.SourceWarehouseId
                    ?? item?.DefaultWarehouseId
                    ?? group?.DefaultWarehouseId;

                // Per ERPNext commits e20967e15e, 8ae5e72ea6, 2c673e4f7e, fa6a517af9, and b9fa398013:
                // BOM rows are costed per row UOM; Work Order rows require stock qty and full-precision rate per stock UOM
                // (rounding rate to 4dp zeroed sub-cent component costs when multiplied by large quantities).
                var convFactor = i.ConversionFactor > 0 ? i.ConversionFactor : 1m;
                var rowStockQty = Math.Round(i.Quantity * convFactor, 4);
                var rowStockRate = i.Rate / convFactor;

                collected.Add(new WorkOrderMaterialRequirement
                {
                    ItemId = i.ItemId,
                    ItemName = i.ItemName,
                    RequiredQty = (rowStockQty / bomOutputQty) * produceQty,
                    Rate = rowStockRate,
                    SourceWarehouseId = sourceWarehouseId
                });
            }
        }

        visitedBoms.Remove(bomId);
    }

    /// <summary>
    /// Resolves default Target/FG warehouse for a Work Order item using hierarchy:
    /// BOM TargetWarehouseId -> Company DefaultFgWarehouseId -> Item DefaultWarehouseId -> ItemGroup DefaultWarehouseId.
    /// Per ERPNext PR #58231 &amp; PR #61652.
    /// </summary>
    public async Task<Guid?> ResolveDefaultFgWarehouseAsync(
        Guid itemId, Guid? companyDefaultFgWarehouseId, IRepository<ItemGroup, Guid>? itemGroupRepository = null, Guid? bomTargetWarehouseId = null)
    {
        if (bomTargetWarehouseId.HasValue)
            return bomTargetWarehouseId;

        if (companyDefaultFgWarehouseId.HasValue)
            return companyDefaultFgWarehouseId;

        var item = await _itemRepository.FindAsync(itemId);
        if (item?.DefaultWarehouseId != null)
            return item.DefaultWarehouseId;

        if (item?.ItemGroupId != null && itemGroupRepository != null)
        {
            var itemGroup = await itemGroupRepository.FindAsync(item.ItemGroupId.Value);
            if (itemGroup?.DefaultWarehouseId != null)
                return itemGroup.DefaultWarehouseId;
        }

        return null;
    }

    /// <summary>
    /// Validates sufficient raw material stock exists before production.
    /// Checks ALL materials first — prevents partial consumption that leaves
    /// inventory in an inconsistent state.
    /// </summary>
    public async Task ValidateRawMaterialAvailabilityAsync(
        WorkOrderMaterialRequirement[] requirements,
        Func<Guid, Guid?, Task<decimal>> getAvailableQty)
    {
        foreach (var req in requirements)
        {
            var available = await getAvailableQty(req.ItemId, req.SourceWarehouseId);
            if (available < req.RequiredQty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.InsufficientRawMaterial)
                    .WithData("itemId", req.ItemId)
                    .WithData("warehouseId", req.SourceWarehouseId?.ToString() ?? "default")
                    .WithData("required", req.RequiredQty)
                    .WithData("available", available);
            }
        }
    }

    /// <summary>
    /// Validates sufficient raw material stock exists before production using batch lookup.
    /// Per ERPNext PR #60200 (commit 0646a4147b): batch work order stock availability.
    /// </summary>
    public async Task ValidateRawMaterialAvailabilityAsync(
        WorkOrderMaterialRequirement[] requirements,
        Func<Dictionary<Guid, HashSet<Guid>>, Task<Dictionary<(Guid ItemId, Guid WarehouseId), decimal>>> getAvailableQtysBatch)
    {
        var itemsByWarehouse = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var req in requirements)
        {
            if (req.SourceWarehouseId.HasValue)
            {
                if (!itemsByWarehouse.TryGetValue(req.SourceWarehouseId.Value, out var set))
                {
                    set = new HashSet<Guid>();
                    itemsByWarehouse[req.SourceWarehouseId.Value] = set;
                }
                set.Add(req.ItemId);
            }
        }

        var availableMap = await getAvailableQtysBatch(itemsByWarehouse);

        foreach (var req in requirements)
        {
            var available = 0m;
            if (req.SourceWarehouseId.HasValue &&
                availableMap.TryGetValue((req.ItemId, req.SourceWarehouseId.Value), out var qty))
            {
                available = qty;
            }

            if (available < req.RequiredQty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.InsufficientRawMaterial)
                    .WithData("itemId", req.ItemId)
                    .WithData("warehouseId", req.SourceWarehouseId?.ToString() ?? "default")
                    .WithData("required", req.RequiredQty)
                    .WithData("available", available);
            }
        }
    }

    /// <summary>
    /// Sets available stock quantities at source warehouse and WIP warehouse for all Work Order items in batch.
    /// Per ERPNext PR #60200 (commit 0646a4147b): RequiredItemsService.set_available_qty using get_latest_stock_qty_for_items.
    /// </summary>
    public async Task SetAvailableQuantitiesAsync(
        WorkOrder workOrder,
        Func<Dictionary<Guid, HashSet<Guid>>, Task<Dictionary<(Guid ItemId, Guid WarehouseId), decimal>>> getAvailableQtysBatch)
    {
        var itemsByWarehouse = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var item in workOrder.RequiredItems)
        {
            if (item.SourceWarehouseId.HasValue)
            {
                if (!itemsByWarehouse.TryGetValue(item.SourceWarehouseId.Value, out var set))
                {
                    set = new HashSet<Guid>();
                    itemsByWarehouse[item.SourceWarehouseId.Value] = set;
                }
                set.Add(item.ItemId);
            }

            if (workOrder.WipWarehouseId.HasValue)
            {
                if (!itemsByWarehouse.TryGetValue(workOrder.WipWarehouseId.Value, out var set))
                {
                    set = new HashSet<Guid>();
                    itemsByWarehouse[workOrder.WipWarehouseId.Value] = set;
                }
                set.Add(item.ItemId);
            }
        }

        var availableQtys = await getAvailableQtysBatch(itemsByWarehouse);

        foreach (var item in workOrder.RequiredItems)
        {
            if (item.SourceWarehouseId.HasValue &&
                availableQtys.TryGetValue((item.ItemId, item.SourceWarehouseId.Value), out var srcQty))
            {
                item.AvailableQtyAtSourceWarehouse = srcQty;
            }
            else
            {
                item.AvailableQtyAtSourceWarehouse = 0m;
            }

            if (workOrder.WipWarehouseId.HasValue &&
                availableQtys.TryGetValue((item.ItemId, workOrder.WipWarehouseId.Value), out var wipQty))
            {
                item.AvailableQtyAtWipWarehouse = wipQty;
            }
            else
            {
                item.AvailableQtyAtWipWarehouse = 0m;
            }
        }
    }

    /// <summary>
    /// Gets the effective backflush method for a Work Order.
    /// Per-BOM setting takes precedence over global ManufacturingSettings.
    /// </summary>
    public async Task<string> GetBackflushMethodAsync(Guid bomId, Guid companyId)
    {
        var bom = await _bomRepository.GetAsync(bomId);

        // Per-BOM override takes precedence
        if (!string.IsNullOrWhiteSpace(bom.BackflushBasedOn))
            return bom.BackflushBasedOn;

        // Fall back to ManufacturingSettings
        var settings = await _settingsRepository
            .FindAsync(s => s.CompanyId == companyId);

        return settings?.BackflushRawMaterialsBasedOn ?? "BOM";
    }

    /// <summary>
    /// Validates that manufacturing warehouses (WIP, FG, Source, Scrap) belong to the same company
    /// as the Work Order. Per ERPNext PR #57540: scope manufacturing warehouse filters to company.
    /// Also validates that group warehouses are not used (per DO-NOT rules).
    /// </summary>
    public async Task ValidateWarehouseCompanyAsync(
        WorkOrder wo,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        var warehouseIds = new[] { wo.SourceWarehouseId, wo.WipWarehouseId, wo.FgWarehouseId, wo.ScrapWarehouseId }
            .Concat(wo.RequiredItems.Select(r => r.SourceWarehouseId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        if (!warehouseIds.Any()) return;

        var queryable = await warehouseRepository.GetQueryableAsync();
        var warehouses = queryable
            .Where(w => warehouseIds.Contains(w.Id))
            .ToList();

        foreach (var wh in warehouses)
        {
            // Company scope check (PR #57540)
            if (wh.CompanyId != wo.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.WorkOrderWarehouseCompanyMismatch)
                    .WithData("warehouse", wh.Name)
                    .WithData("warehouseCompany", wh.CompanyId)
                    .WithData("workOrderCompany", wo.CompanyId);
            }

            // Group warehouse restriction (per DO-NOT: group warehouses cannot receive stock)
            if (wh.IsGroup)
            {
                throw new BusinessException(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock)
                    .WithData("warehouse", wh.Name);
            }
        }
    }

    /// <summary>
    /// Validates mandatory warehouses before Work Order submit.
    /// WIP Warehouse is required unless skipTransfer is true.
    /// Target Warehouse (FgWarehouseId) is required UNLESS TrackSemiFinishedGoods is true (PR #9df527bf3f).
    /// </summary>
    public void ValidateMandatoryWarehouses(WorkOrder wo, bool skipTransfer = false)
    {
        if (!wo.WipWarehouseId.HasValue && !skipTransfer)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Work-in-Progress Warehouse is required before submit.");
        }

        if (!wo.FgWarehouseId.HasValue && !wo.TrackSemiFinishedGoods)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Target Warehouse is required before submit.");
        }
    }

    /// <summary>
    /// Validates Work Order quantity against linked Production Plan planned quantity with overproduction tolerance and process loss headroom.
    /// Per ERPNext PR #58799 & #58847.
    /// </summary>
    public async Task ValidateProductionPlanQuantityAsync(
        WorkOrder wo,
        IRepository<ProductionPlan, Guid> planRepository,
        IRepository<WorkOrder, Guid> workOrderRepository,
        decimal overproductionPercentage)
    {
        if (!wo.ProductionPlanId.HasValue)
            return;

        if (wo.ProductionPlanItemId.HasValue && wo.ProductionPlanSubAssemblyItemId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Work Order must reference only one Production Plan row.");
        }

        if (!wo.ProductionPlanItemId.HasValue && !wo.ProductionPlanSubAssemblyItemId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Work Order with Production Plan must reference a plan item or sub-assembly item.");
        }

        var plan = await planRepository.GetAsync(wo.ProductionPlanId.Value, includeDetails: true);

        decimal plannedQty;
        if (wo.ProductionPlanItemId.HasValue)
        {
            var planItem = plan.PlannedItems.FirstOrDefault(i => i.Id == wo.ProductionPlanItemId.Value);
            if (planItem == null)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Work Order references missing Production Plan Item {wo.ProductionPlanItemId.Value}.");
            }
            plannedQty = planItem.PlannedQty;
        }
        else
        {
            var mrItem = plan.MaterialRequirements.FirstOrDefault(i => i.Id == wo.ProductionPlanSubAssemblyItemId!.Value);
            if (mrItem == null)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Work Order references missing Production Plan Sub Assembly Item {wo.ProductionPlanSubAssemblyItemId!.Value}.");
            }
            plannedQty = mrItem.PlannedQty > 0 ? mrItem.PlannedQty : mrItem.RequiredQty;
        }

        var woQuery = await workOrderRepository.GetQueryableAsync();
        var otherWos = woQuery.Where(w => w.ProductionPlanId == wo.ProductionPlanId.Value
            && w.Id != wo.Id
            && w.Status != WorkOrderStatus.Draft
            && w.Status != WorkOrderStatus.Cancelled)
            .ToList();

        decimal committedQty = 0m;
        if (wo.ProductionPlanItemId.HasValue)
        {
            committedQty = otherWos
                .Where(w => w.ProductionPlanItemId == wo.ProductionPlanItemId.Value)
                .Sum(w => w.Quantity - w.ProcessLossQty);
        }
        else
        {
            committedQty = otherWos
                .Where(w => w.ProductionPlanSubAssemblyItemId == wo.ProductionPlanSubAssemblyItemId!.Value)
                .Sum(w => w.Quantity - w.ProcessLossQty);
        }

        var maxAllowedQty = Math.Round(plannedQty * (1m + overproductionPercentage / 100m) - committedQty + wo.ProcessLossQty, 4);
        if (wo.Quantity > maxAllowedQty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ProductionPlanQuantityExceeded)
                .WithData("workOrderQty", wo.Quantity)
                .WithData("maxAllowedQty", Math.Max(0, maxAllowedQty))
                .WithData("plannedQty", plannedQty)
                .WithData("committedQty", committedQty)
                .WithData("processLossQty", wo.ProcessLossQty);
        }
    }

    /// <summary>
    /// Refreshes the Bin's planned quantity for a given finished good and warehouse.
    /// Per ERPNext get_planned_qty / PR #59419 (commit d687024b88):
    /// Planned qty is calculated AFTER status updates (e.g. Completed/Cancelled/InProcess).
    /// Sum of (Quantity - ProducedQuantity) where Status is Submitted, NotStarted, or InProcess,
    /// and Quantity > ProducedQuantity.
    /// </summary>
    public async Task RefreshPlannedQtyAsync(
        IRepository<WorkOrder, Guid> workOrderRepository,
        Inventory.DomainServices.BinService binService,
        Guid itemId,
        Guid warehouseId,
        Guid? tenantId = null)
    {
        var query = await workOrderRepository.GetQueryableAsync();
        var activeOrders = query
            .Where(w => w.ItemId == itemId
                && w.FgWarehouseId == warehouseId
                && (w.Status == WorkOrderStatus.Submitted || w.Status == WorkOrderStatus.NotStarted || w.Status == WorkOrderStatus.InProcess)
                && (w.Quantity > w.ProducedQuantity + w.ProcessLossQty))
            .ToList();

        var plannedQty = activeOrders.Sum(w => w.Quantity - w.ProducedQuantity - w.ProcessLossQty);
        await binService.SetPlannedQtyAsync(itemId, warehouseId, Math.Max(0m, plannedQty), tenantId);
    }

    /// <summary>
    /// Updates actual start date, actual end date, and lead time on a Work Order.
    /// Derives dates from Job Cards if operations/Job Cards exist, otherwise derives from submitted Stock Entries
    /// (Material Transfer for Manufacture or Manufacture).
    /// Per ERPNext PR #60196 (commit 1e3b9e39be / e3306050b5).
    /// </summary>
    public async Task UpdateActualDatesAsync(
        WorkOrder workOrder,
        IRepository<StockEntry, Guid> stockEntryRepository,
        IRepository<JobCard, Guid>? jobCardRepository = null,
        StockEntry? currentStockEntry = null)
    {
        bool hasJobCards = false;
        if (jobCardRepository != null)
        {
            var jcQuery = await jobCardRepository.GetQueryableAsync();
            var jobCards = jcQuery
                .Where(j => j.WorkOrderId == workOrder.Id && j.Status != JobCardStatus.Cancelled)
                .ToList();

            if (jobCards.Count > 0)
            {
                hasJobCards = true;
                var startDates = jobCards
                    .Select(j => j.StartedAt ?? (j.TimeLogs.Count > 0 ? j.TimeLogs.Min(t => t.FromTime) : (DateTime?)null))
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .ToList();

                var actualStartDate = startDates.Count > 0 ? startDates.Min() : (DateTime?)null;

                DateTime? actualEndDate = null;
                if (workOrder.Status == WorkOrderStatus.Completed)
                {
                    var endDates = jobCards
                        .Select(j => j.CompletedAt ?? (j.TimeLogs.Count > 0 ? j.TimeLogs.Max(t => t.ToTime) : (DateTime?)null))
                        .Where(d => d.HasValue)
                        .Select(d => d!.Value)
                        .ToList();

                    if (endDates.Count > 0)
                    {
                        actualEndDate = endDates.Max();
                    }
                }

                workOrder.SetActualDates(actualStartDate, actualEndDate);
            }
        }

        if (!hasJobCards)
        {
            var seQuery = await stockEntryRepository.GetQueryableAsync();
            var submittedEntries = seQuery
                .Where(se => se.WorkOrderId == workOrder.Id
                    && se.Status == Core.DocumentStatus.Posted
                    && (se.EntryType == StockEntryType.MaterialTransferForManufacture || se.EntryType == StockEntryType.Manufacture))
                .ToList();

            if (currentStockEntry != null && currentStockEntry.WorkOrderId == workOrder.Id)
            {
                if (currentStockEntry.Status != Core.DocumentStatus.Posted)
                {
                    submittedEntries.RemoveAll(se => se.Id == currentStockEntry.Id);
                }
                else if (!submittedEntries.Any(se => se.Id == currentStockEntry.Id))
                {
                    submittedEntries.Add(currentStockEntry);
                }
            }

            if (submittedEntries.Count > 0)
            {
                var postingDates = submittedEntries.Select(se => se.PostingDate).ToList();
                var actualStartDate = postingDates.Min();
                var actualEndDate = workOrder.Status == WorkOrderStatus.Completed ? postingDates.Max() : (DateTime?)null;
                workOrder.SetActualDates(actualStartDate, actualEndDate);
            }
            else
            {
                workOrder.SetActualDates(null, null);
            }
        }
    }

    /// <summary>
    /// Calculates effective Work Order quantity counting against a Sales Order.
    /// Per ERPNext PR #60647 / commit 5b66df2033:
    /// Closed Work Orders count only their ProducedQuantity against the Sales Order.
    /// </summary>
    public static decimal GetWorkOrderQtyAgainstSalesOrder(WorkOrder wo, bool forOpenWorkOrderQty = false)
    {
        if (wo.Status == WorkOrderStatus.Closed)
        {
            return wo.ProducedQuantity;
        }
        return forOpenWorkOrderQty ? (wo.Quantity - wo.ProcessLossQty) : wo.Quantity;
    }

    /// <summary>
    /// Validates that total Work Order quantities against a Sales Order do not exceed the Sales Order quantity + allowance.
    /// Per ERPNext PR #60647 (services/status.py: validate_work_order_against_so).
    /// </summary>
    public async Task ValidateWorkOrderAgainstSalesOrderAsync(
        WorkOrder wo,
        IRepository<Sales.Entities.SalesOrder, Guid> salesOrderRepository,
        IRepository<WorkOrder, Guid> workOrderRepository,
        decimal overproductionPercentageForSalesOrder = 0m)
    {
        if (!wo.SalesOrderId.HasValue) return;

        var so = await salesOrderRepository.FindAsync(wo.SalesOrderId.Value, includeDetails: true);
        if (so == null) return;

        var woQuery = await workOrderRepository.GetQueryableAsync();
        var otherWos = woQuery
            .Where(w => w.SalesOrderId == wo.SalesOrderId.Value
                && w.ItemId == wo.ItemId
                && w.Id != wo.Id
                && w.Status != WorkOrderStatus.Draft
                && w.Status != WorkOrderStatus.Cancelled)
            .ToList();

        // Per ERPNext PR #60647 (commit 025e87f906): filter other work orders by SalesOrderItemId when present
        if (wo.SalesOrderItemId.HasValue)
        {
            otherWos = otherWos.Where(w => w.SalesOrderItemId == wo.SalesOrderItemId.Value).ToList();
        }

        var orderedQtyAgainstSo = otherWos.Sum(w => GetWorkOrderQtyAgainstSalesOrder(w, forOpenWorkOrderQty: true));
        var totalQty = orderedQtyAgainstSo + (wo.Quantity - wo.ProcessLossQty);

        decimal soItemQty = 0m;
        if (wo.SalesOrderItemId.HasValue)
        {
            var row = so.Items.FirstOrDefault(i => i.Id == wo.SalesOrderItemId.Value);
            if (row != null) soItemQty = row.StockQty > 0 ? row.StockQty : row.Quantity;
        }
        else
        {
            soItemQty = so.Items.Where(i => i.ItemId == wo.ItemId).Sum(i => i.StockQty > 0 ? i.StockQty : i.Quantity);
        }

        var maxAllowed = soItemQty * (1m + overproductionPercentageForSalesOrder / 100m);
        if (totalQty > maxAllowed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.WorkOrderOverproduction)
                .WithData("totalQty", totalQty)
                .WithData("maxAllowed", maxAllowed)
                .WithData("salesOrderQty", soItemQty)
                .WithData("orderedQtyAgainstSo", orderedQtyAgainstSo)
                .WithData("detail", $"Cannot produce more than Sales Order quantity {soItemQty}.");
        }
    }

    /// <summary>
    /// Updates WorkOrderQty and ProducedQty on SalesOrderItem rows linked to this Sales Order.
    /// Per ERPNext PR #60647 (commits 5b66df2033 & 025e87f906).
    /// </summary>
    public async Task UpdateSalesOrderWorkOrderQtyAsync(
        Guid salesOrderId,
        IRepository<Sales.Entities.SalesOrder, Guid> salesOrderRepository,
        IRepository<WorkOrder, Guid> workOrderRepository)
    {
        var so = await salesOrderRepository.FindAsync(salesOrderId, includeDetails: true);
        if (so == null) return;

        var woQuery = await workOrderRepository.GetQueryableAsync();
        var submittedWos = woQuery
            .Where(w => w.SalesOrderId == salesOrderId
                && w.Status != WorkOrderStatus.Draft
                && w.Status != WorkOrderStatus.Cancelled)
            .ToList();

        foreach (var item in so.Items)
        {
            var matchingWos = submittedWos
                .Where(w => (w.SalesOrderItemId.HasValue && w.SalesOrderItemId.Value == item.Id)
                         || (!w.SalesOrderItemId.HasValue && w.ItemId == item.ItemId))
                .ToList();

            item.WorkOrderQty = matchingWos.Sum(w => GetWorkOrderQtyAgainstSalesOrder(w, forOpenWorkOrderQty: false));
            item.ProducedQty = matchingWos.Sum(w => w.ProducedQuantity);
        }

        await salesOrderRepository.UpdateAsync(so);
    }

    /// <summary>
    /// Distributes transferred quantity across Work Order required item rows matching the item and source warehouse.
    /// Matches rows with the same SourceWarehouseId first, then distributes proportionally by RequiredQuantity.
    /// If no rows match the warehouse, falls back to all rows of that item.
    /// Per ERPNext PR #60647 (commits 90bc4f1f68, a5ad71eb25, a036285538 / distribute_transfers_over_rows).
    /// </summary>
    public static void DistributeTransferredQuantity(
        WorkOrder wo,
        Guid itemId,
        Guid? sourceWarehouseId,
        decimal quantity,
        bool isDeduction = false)
    {
        var itemRows = wo.RequiredItems.Where(r => r.ItemId == itemId).ToList();
        if (!itemRows.Any()) return;

        var matchingRows = (sourceWarehouseId.HasValue
            ? itemRows.Where(r => r.SourceWarehouseId == sourceWarehouseId.Value).ToList()
            : new List<WorkOrderItem>());

        var targetRows = matchingRows.Count > 0 ? matchingRows : itemRows;
        var totalRequired = targetRows.Sum(r => r.RequiredQuantity);

        var remaining = quantity;
        for (int i = 0; i < targetRows.Count; i++)
        {
            var row = targetRows[i];
            decimal share;
            if (i == targetRows.Count - 1)
            {
                share = remaining;
            }
            else
            {
                share = totalRequired > 0
                    ? Math.Round(quantity * (row.RequiredQuantity / totalRequired), 4)
                    : 0m;
                remaining -= share;
            }

            if (isDeduction)
            {
                row.TransferredQuantity = Math.Max(0m, row.TransferredQuantity - share);
            }
            else
            {
                row.TransferredQuantity += share;
            }
        }
    }

    /// <summary>
    /// Distributes consumed quantity across Work Order required item rows matching the item and source warehouse.
    /// Matches rows with the same SourceWarehouseId first, then distributes proportionally by RequiredQuantity.
    /// If no rows match the warehouse, falls back to all rows of that item.
    /// Per ERPNext PR #60647 (commits 8c663d9608, 4d809d375a, 260b828b09 / _consumed_qty_by_item and distribute_over_rows).
    /// </summary>
    public static void DistributeConsumedQuantity(
        WorkOrder wo,
        Guid itemId,
        Guid? sourceWarehouseId,
        decimal quantity,
        bool isDeduction = false)
    {
        var itemRows = wo.RequiredItems.Where(r => r.ItemId == itemId).ToList();
        if (!itemRows.Any()) return;

        var matchingRows = (sourceWarehouseId.HasValue
            ? itemRows.Where(r => r.SourceWarehouseId == sourceWarehouseId.Value).ToList()
            : new List<WorkOrderItem>());

        var targetRows = matchingRows.Count > 0 ? matchingRows : itemRows;
        var totalRequired = targetRows.Sum(r => r.RequiredQuantity);

        var remaining = quantity;
        for (int i = 0; i < targetRows.Count; i++)
        {
            var row = targetRows[i];
            decimal share;
            if (i == targetRows.Count - 1)
            {
                share = remaining;
            }
            else
            {
                share = totalRequired > 0
                    ? Math.Round(quantity * (row.RequiredQuantity / totalRequired), 4)
                    : 0m;
                remaining -= share;
            }

            if (isDeduction)
            {
                row.ConsumedQuantity = Math.Max(0m, row.ConsumedQuantity - share);
            }
            else
            {
                row.ConsumedQuantity += share;
            }
        }
    }

    /// <summary>
    /// Validates serial number series and automatic batch creation settings when
    /// MakeSerialNoBatchFromWorkOrder is enabled in Manufacturing Settings.
    /// Per ERPNext PR #62430 (commits 52f1f5268c &amp; 5fe3b81f32).
    /// </summary>
    public async Task ValidateSerialAndBatchRequirementsAsync(WorkOrder workOrder)
    {
        var settings = await _settingsRepository.FindAsync(s => s.CompanyId == workOrder.CompanyId);
        if (settings == null || !settings.MakeSerialNoBatchFromWorkOrder)
            return;

        var item = await _itemRepository.GetAsync(workOrder.ItemId);

        if (item.HasBatchNo && !item.CreateNewBatch)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Set Automatically Create New Batch in Item '{item.ItemCode}' or disable Make Serial No / Batch from Work Order in Manufacturing Settings.");
        }

        if (item.HasSerialNo && string.IsNullOrWhiteSpace(item.SerialNoSeries))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Set Serial No Series in Item '{item.ItemCode}' or disable Make Serial No / Batch from Work Order in Manufacturing Settings.");
        }
    }

    /// <summary>
    /// Returns true when a Job Card produces a semi-finished intermediate good rather than the Work Order's final finished good.
    /// Per ERPNext PR #68701 (commit a96f709df4 / operations_cost.makes_semi_finished_good).
    /// </summary>
    public static bool MakesSemiFinishedGood(Guid workOrderItemId, Guid? jobCardFinishedGoodItemId)
    {
        return jobCardFinishedGoodItemId.HasValue && jobCardFinishedGoodItemId.Value != workOrderItemId;
    }

    /// <summary>
    /// Calculates the maximum completed operation quantity eligible to absorb Work Order corrective operation cost.
    /// Per ERPNext PR #68701 (commit ac5cc09181 / operations_cost._max_operation_quantity): when
    /// <see cref="WorkOrder.TrackSemiFinishedGoods"/> is enabled, only Job Cards producing the Work Order's final
    /// finished good (<see cref="WorkOrder.ItemId"/>) are counted.
    /// </summary>
    public static decimal CalculateMaxOperationQuantity(
        WorkOrder workOrder,
        IEnumerable<JobCard> submittedJobCards)
    {
        var eligible = submittedJobCards
            .Where(jc => jc.WorkOrderId == workOrder.Id
                && jc.Status == JobCardStatus.Completed
                && !jc.IsCorrective);

        if (workOrder.TrackSemiFinishedGoods)
        {
            eligible = eligible.Where(jc => jc.FinishedGoodItemId == workOrder.ItemId);
        }

        var perOperationTotals = eligible
            .GroupBy(jc => jc.OperationId)
            .Select(g => g.Sum(jc => jc.CompletedQty))
            .ToList();

        return perOperationTotals.Count > 0 ? perOperationTotals.Min() : 0m;
    }

    /// <summary>
    /// Computes the corrective operation cost to allocate onto a Manufacture Stock Entry.
    /// Per ERPNext PR #68701 (commits a96f709df4, ac5cc09181): skips semi-finished good entries and spreads
    /// remaining corrective cost strictly over finished good units.
    /// </summary>
    public static decimal CalculateCorrectiveOperationCostForManufacture(
        WorkOrder workOrder,
        decimal correctiveOperationCost,
        decimal utilisedCorrectiveCost,
        decimal fgCompletedQty,
        IEnumerable<JobCard> submittedJobCards,
        JobCard? currentJobCard = null,
        bool correctiveCostEnabled = true,
        int precision = 4)
    {
        if (!correctiveCostEnabled || correctiveOperationCost <= 0m || fgCompletedQty <= 0m)
        {
            return 0m;
        }

        if (MakesSemiFinishedGood(workOrder.ItemId, currentJobCard?.FinishedGoodItemId))
        {
            return 0m;
        }

        var remaining = Math.Max(0m, correctiveOperationCost - utilisedCorrectiveCost);
        if (remaining <= 0m)
        {
            return 0m;
        }

        var maxQty = CalculateMaxOperationQuantity(workOrder, submittedJobCards) - workOrder.ProducedQuantity;
        if (maxQty <= 0m)
        {
            maxQty = fgCompletedQty;
        }

        return Math.Round((remaining / maxQty) * fgCompletedQty, precision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Allocates Work Order operation costs onto a Manufacture Stock Entry per ERPNext PR #67334
    /// (commits 437afa2d63, d51959d7a4, 205f50785b in <c>bom/services/operations_cost.py</c>):
    /// - Resolves operating components from <c>Workstation</c> when assigned, or from <c>WorkstationType</c>
    ///   when an operation is planned on a Workstation Type without a specific Workstation.
    /// - Operations whose workstation/type has no positive-rate operating components are collected in
    ///   <c>withoutComponents</c>; when at least one operation adds component-wise costs, those remaining
    ///   operations are still booked as per-operation operating cost rows instead of being silently dropped.
    /// - When no operation has positive-rate operating components, returns per-operation cost entries for all
    ///   operations with positive <c>ActualOperationTime</c>.
    /// </summary>
    public static IReadOnlyList<WorkOrderOperationCostEntry> CalculateOperationOperatingCosts(
        IEnumerable<WorkOrderOperationCostInput> operations,
        Func<Guid, IReadOnlyList<(string Component, decimal OperatingCost, Guid? AccountId)>>? getWorkstationComponents,
        Func<Guid, IReadOnlyList<(string Component, decimal OperatingCost, Guid? AccountId)>>? getWorkstationTypeComponents,
        Guid? defaultOperatingExpenseAccountId = null,
        Guid? filterOperationId = null,
        int precision = 4)
    {
        var entries = new List<WorkOrderOperationCostEntry>();
        var withoutComponents = new List<WorkOrderOperationCostInput>();
        var componentCostAdded = false;

        foreach (var op in operations ?? Array.Empty<WorkOrderOperationCostInput>())
        {
            if (filterOperationId.HasValue && op.OperationId != filterOperationId.Value)
                continue;
            if (op.ActualOperationTime <= 0m)
                continue;

            IReadOnlyList<(string Component, decimal OperatingCost, Guid? AccountId)> rawComponents =
                op.WorkstationId.HasValue && getWorkstationComponents != null
                    ? getWorkstationComponents(op.WorkstationId.Value)
                    : (op.WorkstationTypeId.HasValue && getWorkstationTypeComponents != null
                        ? getWorkstationTypeComponents(op.WorkstationTypeId.Value)
                        : Array.Empty<(string, decimal, Guid?)>());

            var positiveComponents = rawComponents
                .Where(c => c.OperatingCost > 0m)
                .ToList();

            var totalComponentRate = positiveComponents.Sum(c => c.OperatingCost);
            if (positiveComponents.Count == 0 || op.ActualOperatingCost <= 0m || totalComponentRate <= 0m)
            {
                withoutComponents.Add(op);
                continue;
            }

            foreach (var comp in positiveComponents)
            {
                var share = Math.Round(op.ActualOperatingCost * (comp.OperatingCost / totalComponentRate), precision, MidpointRounding.AwayFromZero);
                if (share > 0m)
                {
                    entries.Add(new WorkOrderOperationCostEntry(
                        op.OperationId,
                        op.OperationName,
                        comp.Component,
                        comp.AccountId ?? defaultOperatingExpenseAccountId,
                        share,
                        IsComponentWise: true));
                    componentCostAdded = true;
                }
            }
        }

        // Per PR #67334: whether component-wise costs were added on sibling operations or not,
        // operations without positive-rate components are still costed per operation.
        var fallbackOps = componentCostAdded ? withoutComponents : withoutComponents;
        foreach (var op in fallbackOps)
        {
            if (op.ActualOperatingCost > 0m)
            {
                entries.Add(new WorkOrderOperationCostEntry(
                    op.OperationId,
                    op.OperationName,
                    OperatingComponent: null,
                    defaultOperatingExpenseAccountId,
                    Math.Round(op.ActualOperatingCost, precision, MidpointRounding.AwayFromZero),
                    IsComponentWise: false));
            }
        }

        return entries;
    }

    /// <summary>
    /// Determines whether a Stock Entry counts toward the Work Order's Pick List transfer check:
    /// requires a submitted, non-return, non-additional <see cref="StockEntryType.MaterialTransferForManufacture"/>
    /// with <c>fgCompletedQty == 0</c> and no linked <c>jobCardId</c>, so additional transfer entries do not
    /// skew Pick List required-item checks.
    /// Per ERPNext commit <c>2511433013</c> (<c>work_order/services/required_items.py</c>).
    /// </summary>
    public static bool IsEligibleForWorkOrderPickListTransferCheck(
        bool isSubmitted,
        StockEntryType entryType,
        bool isReturn,
        bool isAdditionalTransferEntry,
        decimal fgCompletedQty,
        Guid? jobCardId)
    {
        return isSubmitted
            && entryType == StockEntryType.MaterialTransferForManufacture
            && !isReturn
            && !isAdditionalTransferEntry
            && fgCompletedQty == 0m
            && !jobCardId.HasValue;
    }

    /// <summary>
    /// Computes the proposed BOM-based Material Consumption quantity for a Work Order by flooring
    /// <c>min(leftForQty, workOrderQty - producedQty - processLossQty)</c> at <paramref name="qtyPrecision"/>
    /// (rounding down, never up) so the consumption proposal never requests more than remains unconsumed.
    /// Per ERPNext commit <c>9ebd625556</c> (<c>work_order/mapper.py</c>).
    /// </summary>
    public static decimal CalculatePendingConsumptionQtyBasedOnBom(
        decimal leftForQty,
        decimal workOrderQty,
        decimal producedQty,
        decimal processLossQty,
        int qtyPrecision = 4)
    {
        var pendingToProduce = workOrderQty - producedQty - processLossQty;
        var raw = Math.Min(leftForQty, pendingToProduce);
        if (raw <= 0m)
        {
            return 0m;
        }

        var step = 1m;
        for (var i = 0; i < qtyPrecision; i++)
        {
            step *= 10m;
        }

        var scaled = Math.Round(raw * step, 9, MidpointRounding.AwayFromZero);
        return Math.Max(Math.Floor(scaled) / step, 0m);
    }

    /// <summary>
    /// Computes planned operation time per unit for a Work Order operation per ERPNext PR #67331
    /// (<c>work_order/services/operations.py: get_operation_time_per_unit</c>):
    /// treats the operation as per-batch when EITHER the Operation master has
    /// <c>create_job_card_based_on_batch_size</c> OR the BOM row has <c>set_cost_based_on_bom_qty</c>.
    /// </summary>
    public static decimal CalculateOperationTimePerUnit(
        decimal timeInMins,
        decimal qty,
        decimal units,
        bool exploded,
        bool operationBatchSizeFlag,
        bool setCostBasedOnBomQty,
        decimal batchSize)
    {
        var isPerBatch = operationBatchSizeFlag || setCostBasedOnBomQty;
        if (isPerBatch && batchSize > 0m)
        {
            return timeInMins * units / batchSize;
        }

        if (exploded)
        {
            return timeInMins * qty;
        }

        return qty > 0m ? timeInMins / qty : 0m;
    }

    /// <summary>
    /// Computes scheduled operation duration for a Production Plan item quantity
    /// per ERPNext PR #68876 (<c>manufacturing/scheduling/loaders.py: get_operation_duration</c>):
    /// returns fixed time verbatim when <paramref name="fixedTime"/> is set, otherwise scales
    /// <see cref="CalculateOperationTimePerUnit"/> by <paramref name="qty"/> so batch-based operations
    /// schedule per batch rather than dividing by BOM quantity.
    /// </summary>
    public static decimal CalculatePlannedOperationDuration(
        decimal timeInMins,
        decimal qty,
        decimal bomQty,
        bool fixedTime,
        bool isBatchBased,
        decimal batchSize,
        bool setCostBasedOnBomQty = false)
    {
        if (fixedTime)
            return timeInMins;

        return CalculateOperationTimePerUnit(
            timeInMins,
            qty: bomQty,
            units: 1m,
            exploded: false,
            operationBatchSizeFlag: isBatchBased,
            setCostBasedOnBomQty: setCostBasedOnBomQty,
            batchSize: batchSize) * qty;
    }
}

public sealed record WorkOrderOperationCostInput(
    Guid OperationId,
    string OperationName,
    Guid? WorkstationId,
    Guid? WorkstationTypeId,
    decimal ActualOperationTime,
    decimal ActualOperatingCost);

public sealed record WorkOrderOperationCostEntry(
    Guid OperationId,
    string OperationName,
    string? OperatingComponent,
    Guid? ExpenseAccountId,
    decimal Amount,
    bool IsComponentWise);

/// <summary>
/// Represents a calculated material requirement for production.
/// </summary>
public class WorkOrderMaterialRequirement
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal RequiredQty { get; set; }
    public decimal Rate { get; set; }
    public Guid? SourceWarehouseId { get; set; }
}
