using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Supplier Scorecard Variable — catalog of named variables (e.g. total_ordered_qty,
/// total_late_days) that a <see cref="ScorecardCriterion"/> formula can reference.
/// Reference/documentation data only; formulas themselves remain free-text.
/// Maps to ERPNext buying/doctype/supplier_scorecard_variable.
/// </summary>
public class SupplierScorecardVariable : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string VariableLabel { get; set; } = null!;
    public string ParamName { get; set; } = null!;
    public string Path { get; set; } = null!;
    public bool IsCustom { get; set; }
    public string? Description { get; set; }

    protected SupplierScorecardVariable() { }

    public SupplierScorecardVariable(Guid id, string variableLabel, string paramName, string path,
        bool isCustom = false, string? description = null, Guid? tenantId = null)
        : base(id)
    {
        VariableLabel = Check.NotNullOrWhiteSpace(variableLabel, nameof(variableLabel), maxLength: SupplierScorecardVariableConsts.MaxVariableLabelLength);
        ParamName = Check.NotNullOrWhiteSpace(paramName, nameof(paramName), maxLength: SupplierScorecardVariableConsts.MaxParamNameLength);
        Path = Check.NotNullOrWhiteSpace(path, nameof(path), maxLength: SupplierScorecardVariableConsts.MaxPathLength);
        IsCustom = isCustom;
        Description = description;
        TenantId = tenantId;
    }
}
