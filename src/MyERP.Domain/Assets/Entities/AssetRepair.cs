using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

/// <summary>
/// Asset Repair — tracks repair costs with optional capitalization.
/// Per gotcha #35: fully depreciated assets CAN be repaired but
/// capitalize_repair_cost and increase_in_asset_life are forced to 0.
/// Maps to ERPNext assets/doctype/asset_repair.
/// </summary>
public class AssetRepair : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AssetId { get; set; }

    public string? RepairDescription { get; set; }
    public DateTime? FailureDate { get; set; }
    public DateTime? CompletionDate { get; set; }

    /// <summary>Total repair cost (parts + labor).</summary>
    public decimal RepairCost { get; set; }

    /// <summary>When true, repair cost is added to asset value (increases book value).</summary>
    public bool CapitalizeRepairCost { get; set; }

    /// <summary>Additional months added to useful life due to repair.</summary>
    public int IncreaseInAssetLife { get; set; }

    /// <summary>Stock items consumed during repair (for perpetual inventory GL).</summary>
    public decimal StockItemConsumedCost { get; set; }

    public AssetRepairStatus Status { get; private set; } = AssetRepairStatus.Pending;

    protected AssetRepair() { }

    public AssetRepair(Guid id, Guid companyId, Guid assetId, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        AssetId = assetId;
        TenantId = tenantId;
    }

    /// <summary>
    /// Applies fully-depreciated asset rules:
    /// forces CapitalizeRepairCost=false and IncreaseInAssetLife=0.
    /// Per gotcha #35.
    /// </summary>
    public void ApplyFullyDepreciatedRules(bool isFullyDepreciated)
    {
        if (isFullyDepreciated)
        {
            CapitalizeRepairCost = false;
            IncreaseInAssetLife = 0;
        }
    }

    public void Complete()
    {
        if (Status != AssetRepairStatus.Pending)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        CompletionDate ??= DateTime.UtcNow;
        Status = AssetRepairStatus.Completed;
    }

    public void Cancel()
    {
        if (Status == AssetRepairStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = AssetRepairStatus.Cancelled;
    }
}

public enum AssetRepairStatus
{
    Pending = 0,
    Completed = 1,
    Cancelled = 2
}
