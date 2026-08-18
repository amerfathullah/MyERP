using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

/// <summary>
/// Asset Shift Factor master — multiplier applied to depreciation for multi-shift operations
/// (e.g., Single Shift = 1.0, Double Shift = 2.0, Triple Shift = 3.0).
/// Maps to ERPNext assets/doctype/asset_shift_factor.
/// </summary>
public class AssetShiftFactor : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string ShiftName { get; set; } = null!;

    /// <summary>Multiplier applied to a period's base depreciation amount.</summary>
    public decimal Factor { get; set; }

    public bool IsDefault { get; set; }

    protected AssetShiftFactor() { }

    public AssetShiftFactor(Guid id, string shiftName, decimal factor, Guid? tenantId = null) : base(id)
    {
        ShiftName = Check.NotNullOrWhiteSpace(shiftName, nameof(shiftName), AssetShiftFactorConsts.MaxShiftNameLength);
        Factor = factor;
        TenantId = tenantId;
    }
}
