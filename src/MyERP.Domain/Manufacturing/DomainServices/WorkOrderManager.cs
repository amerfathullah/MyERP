using System;
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

                collected.Add(new WorkOrderMaterialRequirement
                {
                    ItemId = i.ItemId,
                    ItemName = i.ItemName,
                    RequiredQty = (i.Quantity / bomOutputQty) * produceQty,
                    Rate = i.Rate,
                    SourceWarehouseId = sourceWarehouseId
                });
            }
        }

        visitedBoms.Remove(bomId);
    }

    /// <summary>
    /// Resolves default Target/FG warehouse for a Work Order item using hierarchy:
    /// Company DefaultFgWarehouseId -> Item DefaultWarehouseId -> ItemGroup DefaultWarehouseId.
    /// Per ERPNext PR #58231.
    /// </summary>
    public async Task<Guid?> ResolveDefaultFgWarehouseAsync(
        Guid itemId, Guid? companyDefaultFgWarehouseId, IRepository<ItemGroup, Guid>? itemGroupRepository = null)
    {
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
                && w.Quantity > w.ProducedQuantity)
            .ToList();

        var plannedQty = activeOrders.Sum(w => w.Quantity - w.ProducedQuantity);
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
}

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
