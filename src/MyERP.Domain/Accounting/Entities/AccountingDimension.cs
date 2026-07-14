using System;
using System.Linq;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Accounting Dimension — configurable custom dimensions for GL entry segmentation.
/// ERPNext equivalent: accounts/doctype/accounting_dimension.
/// 
/// Each dimension maps to a reference doctype (e.g., Department, Branch, Region, Project).
/// When enabled, all transaction doctypes get an extra field for this dimension.
/// GL entries inherit the dimension value and can be filtered/grouped by it in reports.
/// 
/// Examples:
///   - "Department" → links to Department entity
///   - "Branch" → links to Branch entity
///   - "Region" → links to Territory entity
///   - Custom dimensions can be added for business-specific needs
/// </summary>
public class AccountingDimension : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>The reference doctype/entity this dimension points to (e.g., "Branch", "Department").</summary>
    public string DocumentType { get; private set; } = null!;

    /// <summary>Human-readable label for forms and reports.</summary>
    public string Label { get; set; } = null!;

    /// <summary>Field name used in transaction tables (auto-generated from DocumentType).</summary>
    public string FieldName { get; private set; } = null!;

    /// <summary>Whether this dimension is currently active on transactions.</summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>Whether this dimension is mandatory on GL entries.</summary>
    public bool IsMandatory { get; set; }

    /// <summary>
    /// Whether disabled dimension values should be hidden from selection.
    /// When true, only active records of the referenced doctype are shown.
    /// </summary>
    public bool HideDisabledValues { get; set; } = true;

    /// <summary>Company scope. Null = global (applies to all companies).</summary>
    public Guid? CompanyId { get; set; }

    protected AccountingDimension() { }

    public AccountingDimension(Guid id, string documentType, string label, Guid? tenantId = null)
        : base(id)
    {
        DocumentType = documentType ?? throw new ArgumentNullException(nameof(documentType));
        Label = label ?? throw new ArgumentNullException(nameof(label));
        FieldName = GenerateFieldName(documentType);
        TenantId = tenantId;
    }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;

    private static string GenerateFieldName(string documentType)
    {
        // Convert "CostCenter" → "cost_center_id", "Branch" → "branch_id"
        var snakeCase = string.Concat(documentType.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
        return snakeCase + "_id";
    }
}

/// <summary>
/// Accounting Dimension Filter — restricts dimension values per account.
/// ERPNext equivalent: accounts/doctype/accounting_dimension_filter.
/// 
/// When configured, specific accounts can only use allowed dimension values,
/// or can block specific dimension values (allow_or_restrict mode).
/// </summary>
public class AccountingDimensionFilter : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid AccountingDimensionId { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>True = only these values allowed. False = these values blocked (all others allowed).</summary>
    public bool IsAllowList { get; set; } = true;

    /// <summary>Comma-separated list of allowed/blocked dimension value IDs.</summary>
    public string DimensionValueIds { get; set; } = string.Empty;

    /// <summary>Company scope for this filter.</summary>
    public Guid CompanyId { get; set; }

    protected AccountingDimensionFilter() { }

    public AccountingDimensionFilter(Guid id, Guid dimensionId, Guid accountId, Guid companyId, bool isAllowList)
        : base(id)
    {
        AccountingDimensionId = dimensionId;
        AccountId = accountId;
        CompanyId = companyId;
        IsAllowList = isAllowList;
    }

    /// <summary>Checks if a specific dimension value ID is permitted by this filter.</summary>
    public bool IsValuePermitted(Guid valueId)
    {
        if (string.IsNullOrWhiteSpace(DimensionValueIds))
            return !IsAllowList; // Empty allow list = nothing allowed; empty block list = everything allowed

        var ids = DimensionValueIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var contains = Array.Exists(ids, id => Guid.TryParse(id.Trim(), out var parsed) && parsed == valueId);

        return IsAllowList ? contains : !contains;
    }
}
