using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

public class AssetRepairConsumedItem : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid AssetRepairId { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid? WarehouseId { get; set; }
    public decimal Qty { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal TotalValue => Qty * ValuationRate;
    public string? SerialAndBatchBundleId { get; set; }

    protected AssetRepairConsumedItem() { }

    public AssetRepairConsumedItem(
        Guid id,
        Guid assetRepairId,
        Guid itemId,
        decimal qty,
        decimal valuationRate,
        Guid? warehouseId = null,
        string? itemName = null,
        string? serialAndBatchBundleId = null,
        Guid? tenantId = null)
        : base(id)
    {
        AssetRepairId = assetRepairId;
        ItemId = itemId;
        Qty = qty;
        ValuationRate = valuationRate;
        WarehouseId = warehouseId;
        ItemName = itemName;
        SerialAndBatchBundleId = serialAndBatchBundleId;
        TenantId = tenantId;
    }
}
