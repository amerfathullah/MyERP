using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Data-driven contribution rule for EPF, SOCSO, EIS, PCB.
/// NEVER hardcode rates — they change based on government gazette.
/// Supports date-range effectivity and filters for age/citizenship.
/// </summary>
public class ContributionRule : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public ContributionType Type { get; set; }

    /// <summary>Employee contribution rate as percentage (e.g., 11 = 11%).</summary>
    public decimal EmployeeRate { get; set; }

    /// <summary>Employer contribution rate as percentage (e.g., 12 = 12%).</summary>
    public decimal EmployerRate { get; set; }

    /// <summary>Monthly salary ceiling (null = no ceiling).</summary>
    public decimal? SalaryCeiling { get; set; }

    /// <summary>Minimum salary for this rule to apply.</summary>
    public decimal? MinimumSalary { get; set; }

    /// <summary>Maximum salary for this rule to apply.</summary>
    public decimal? MaximumSalary { get; set; }

    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    /// <summary>Filter: applies to employees above/below certain age.</summary>
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    /// <summary>Filter: applies only to specific citizenship type.</summary>
    public CitizenshipType? CitizenshipFilter { get; set; }

    public bool IsActive { get; set; } = true;

    protected ContributionRule() { }

    public ContributionRule(Guid id, ContributionType type, decimal employeeRate, decimal employerRate,
        DateTime effectiveFrom, Guid? tenantId = null)
        : base(id)
    {
        Type = type;
        EmployeeRate = employeeRate;
        EmployerRate = employerRate;
        EffectiveFrom = effectiveFrom;
        TenantId = tenantId;
    }

    public bool IsApplicable(DateTime date, decimal salary, int age, CitizenshipType citizenship)
    {
        if (!IsActive) return false;
        if (date < EffectiveFrom) return false;
        if (EffectiveTo.HasValue && date > EffectiveTo.Value) return false;
        if (MinimumSalary.HasValue && salary < MinimumSalary.Value) return false;
        if (MaximumSalary.HasValue && salary > MaximumSalary.Value) return false;
        if (MinAge.HasValue && age < MinAge.Value) return false;
        if (MaxAge.HasValue && age > MaxAge.Value) return false;
        if (CitizenshipFilter.HasValue && citizenship != CitizenshipFilter.Value) return false;
        return true;
    }
}
