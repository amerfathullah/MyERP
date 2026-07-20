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
            throw new BusinessException("MyERP:10011")
                .WithData("bomItem", bom.ItemId)
                .WithData("woItem", itemId);
        }
    }

    /// <summary>
    /// Gets the effective overproduction percentage from ManufacturingSettings.
    /// Falls back to 0% if no settings exist for the company.
    /// </summary>
    public async Task<decimal> GetOverproductionPercentageAsync(Guid companyId)
    {
        var settings = await _settingsRepository
            .FindAsync(s => s.CompanyId == companyId);

        return settings?.OverproductionPercentage ?? 0m;
    }

    /// <summary>
    /// Calculates proportional raw material quantities for a given production quantity.
    /// bomItem.Quantity × (produceQty / bom.Quantity)
    /// </summary>
    public async Task<WorkOrderMaterialRequirement[]> CalculateMaterialRequirementsAsync(
        Guid bomId, decimal produceQty)
    {
        var bom = await _bomRepository.GetAsync(bomId);

        return bom.Items
            .Where(i => !i.IsPhantom) // phantom items bubble up, don't consume directly
            .Select(i => new WorkOrderMaterialRequirement
            {
                ItemId = i.ItemId,
                RequiredQty = bom.Quantity > 0 ? i.Quantity * (produceQty / bom.Quantity) : 0,
                Rate = i.Rate,
                SourceWarehouseId = i.SourceWarehouseId
            })
            .ToArray();
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
