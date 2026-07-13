using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Salary Component — a configurable building block for salary calculation.
/// Examples: Basic Salary, HRA, EPF Employee, SOCSO, EIS, PCB, Overtime, Commission.
/// Components can be earnings (adds to gross) or deductions (reduces net).
/// Formula-based components use an expression engine for dynamic calculation.
/// </summary>
public class SalaryComponent : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Abbreviation used in formulas (e.g., "B" for Basic, "HRA" for House Rent).</summary>
    public string? Abbreviation { get; set; }

    /// <summary>Earning or Deduction.</summary>
    public SalaryComponentType ComponentType { get; set; }

    /// <summary>If true, this is a statutory deduction (EPF, SOCSO, EIS, PCB).</summary>
    public bool IsStatutory { get; set; }

    /// <summary>If true, this component is included in income tax calculation.</summary>
    public bool IsTaxApplicable { get; set; } = true;

    /// <summary>If true, depends on payment days (prorated for partial months).</summary>
    public bool DependsOnPaymentDays { get; set; } = true;

    /// <summary>GL account for posting this component.</summary>
    public Guid? DefaultAccountId { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Description { get; set; }

    protected SalaryComponent() { }

    public SalaryComponent(Guid id, string name, SalaryComponentType componentType, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 200);
        ComponentType = componentType;
        TenantId = tenantId;
    }
}

public enum SalaryComponentType
{
    Earning = 0,
    Deduction = 1,
}
