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
    /// Free-text, kept for backward compatibility — see CategoryId for the controlled picklist.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>Optional link to the structured UomCategory master (controlled picklist).</summary>
    public Guid? CategoryId { get; set; }

    public bool IsEnabled { get; set; } = true;

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;

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
    /// Per ERPNext PR #57861 / commit a464a6e4a1:
    /// Round to precision first to eliminate conversion factor dust (e.g. 1999.99999 -> 2000),
    /// then verify it equals the rounded integer value.
    /// Throws UOMMustBeIntegerError if fractional.
    /// </summary>
    public void ValidateWholeNumber(decimal qty, int precision = 4)
    {
        if (!MustBeWholeNumber) return;

        var roundedQty = Math.Round(qty, precision);
        if (Math.Abs(roundedQty - Math.Round(roundedQty, 0)) > 0.0000001m)
        {
            throw new BusinessException("MyERP:05029")
                .WithData("uom", Name)
                .WithData("qty", roundedQty);
        }
    }
}
