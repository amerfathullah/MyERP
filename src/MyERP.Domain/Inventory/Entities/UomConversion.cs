using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// UOM Conversion Factor — defines conversion between units of measure.
/// Two types: global (e.g., 1 Kg = 1000 g) and item-specific (e.g., 1 Box of Item A = 12 Units).
/// </summary>
public class UomConversion : AuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Source UOM (e.g., "Box").</summary>
    public string FromUom { get; set; } = null!;

    /// <summary>Target UOM (e.g., "Unit").</summary>
    public string ToUom { get; set; } = null!;

    /// <summary>Conversion factor: 1 FromUom = ConversionFactor ToUom.</summary>
    public decimal ConversionFactor { get; set; }

    /// <summary>If set, this conversion is specific to this item. If null, it's a global conversion.</summary>
    public Guid? ItemId { get; set; }

    protected UomConversion() { }

    public UomConversion(Guid id, string fromUom, string toUom, decimal conversionFactor, Guid? itemId = null, Guid? tenantId = null)
        : base(id)
    {
        FromUom = fromUom;
        ToUom = toUom;
        ConversionFactor = conversionFactor;
        ItemId = itemId;
        TenantId = tenantId;
    }

    /// <summary>Convert a quantity from the source UOM to the target UOM.</summary>
    public decimal Convert(decimal qty) => qty * ConversionFactor;

    /// <summary>Reverse convert (target → source).</summary>
    public decimal ReverseConvert(decimal qty) => ConversionFactor != 0 ? qty / ConversionFactor : 0;
}
