using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Stores accounting dimension values assigned to specific GL entry lines.
/// Each JournalEntryLine can have multiple dimension assignments (one per enabled dimension).
/// This is a junction/bridge table: JournalEntryLine ↔ AccountingDimension ↔ Value.
/// 
/// Example: A JE line for "Office Supplies" expense might have:
///   - DimensionFieldName="branch_id", DimensionValueId="{KL Office branch ID}"
///   - DimensionFieldName="department_id", DimensionValueId="{Marketing dept ID}"
///   
/// Reports can then slice P&L by branch, department, or any combination.
/// </summary>
public class GlDimensionValue : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>The JournalEntryLine this dimension assignment belongs to.</summary>
    public Guid JournalEntryLineId { get; set; }

    /// <summary>The dimension definition (e.g., "Branch", "Department").</summary>
    public Guid AccountingDimensionId { get; set; }

    /// <summary>Field name from AccountingDimension (e.g., "branch_id") for efficient filtering.</summary>
    public string DimensionFieldName { get; set; } = null!;

    /// <summary>The specific dimension value ID (e.g., the Branch entity's ID).</summary>
    public Guid DimensionValueId { get; set; }

    protected GlDimensionValue() { }

    public GlDimensionValue(Guid id, Guid journalEntryLineId, Guid accountingDimensionId,
        string dimensionFieldName, Guid dimensionValueId, Guid? tenantId = null)
        : base(id)
    {
        JournalEntryLineId = journalEntryLineId;
        AccountingDimensionId = accountingDimensionId;
        DimensionFieldName = dimensionFieldName;
        DimensionValueId = dimensionValueId;
        TenantId = tenantId;
    }
}
