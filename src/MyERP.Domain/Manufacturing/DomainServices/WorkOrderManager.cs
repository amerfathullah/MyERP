using System;
using System.Linq;
using System.Threading.Tasks;
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
    /// </summary>
    public async Task<WorkOrderMaterialRequirement[]> CalculateMaterialRequirementsAsync(
        Guid bomId, decimal produceQty, IRepository<ItemGroup, Guid>? itemGroupRepository = null)
    {
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

        return bom.Items
            .Where(i => !i.IsPhantom) // phantom items bubble up, don't consume directly
            .Select(i =>
            {
                itemMap.TryGetValue(i.ItemId, out var item);
                ItemGroup? group = null;
                if (item?.ItemGroupId != null)
                    itemGroupMap.TryGetValue(item.ItemGroupId.Value, out group);

                var sourceWarehouseId = i.SourceWarehouseId
                    ?? item?.DefaultWarehouseId
                    ?? group?.DefaultWarehouseId;

                return new WorkOrderMaterialRequirement
                {
                    ItemId = i.ItemId,
                    RequiredQty = bom.Quantity > 0 ? i.Quantity * (produceQty / bom.Quantity) : 0,
                    Rate = i.Rate,
                    SourceWarehouseId = sourceWarehouseId
                };
            })
            .ToArray();
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
}

/// <summary>
/// Represents a calculated material requirement for production.
/// </summary>
public class WorkOrderMaterialRequirement
{
    public Guid ItemId { get; set; }
    public decimal RequiredQty { get; set; }
    public decimal Rate { get; set; }
    public Guid? SourceWarehouseId { get; set; }
}
