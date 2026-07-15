using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Unit of Measure master record.
/// Maps to ERPNext stock/doctype/uom.
/// </summary>
public class Uom : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>UOM name (e.g., "Unit", "Kg", "Box", "Litre").</summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// When true, quantities in this UOM must be whole numbers.
    /// Tolerance: 0.0000001 for float comparison.
    /// Per DO-NOT: "Allow fractional qty for UOMs with must_be_whole_number=1"
    /// </summary>
    public bool MustBeWholeNumber { get; set; }

    /// <summary>
    /// UOM category for grouping (e.g., Mass, Length, Volume, Time).
    /// Per ERPNext v16: structured category field.
    /// </summary>
    public string? Category { get; set; }

    public bool IsEnabled { get; set; } = true;

    protected Uom() { }

    public Uom(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        SetName(name);
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 50);
    }

    /// <summary>
    /// Validates that a quantity is a whole number (within float tolerance).
    /// Throws UOMMustBeIntegerError if fractional.
    /// </summary>
    public void ValidateWholeNumber(decimal qty)
    {
        if (!MustBeWholeNumber) return;

        var remainder = qty - Math.Floor(qty);
        if (remainder > 0.0000001m)
        {
            throw new BusinessException("MyERP:05029")
                .WithData("uom", Name)
                .WithData("qty", qty);
        }
    }
}
