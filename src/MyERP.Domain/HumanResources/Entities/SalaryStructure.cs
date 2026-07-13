using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Salary Structure — template defining how an employee's salary is calculated.
/// Contains a list of salary detail lines (components + amounts/formulas).
/// Assigned to employees to determine their salary computation.
/// </summary>
public class SalaryStructure : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Whether this structure pays hourly or monthly.</summary>
    public bool IsHourlyBased { get; set; }

    /// <summary>Payroll frequency: Monthly, Bimonthly, Weekly.</summary>
    public string PayrollFrequency { get; set; } = "Monthly";

    public bool IsActive { get; set; } = true;

    public string? Description { get; set; }

    private readonly List<SalaryStructureDetail> _details = new();
    public IReadOnlyList<SalaryStructureDetail> Details => _details.AsReadOnly();

    protected SalaryStructure() { }

    public SalaryStructure(Guid id, Guid companyId, string name, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 200);
        TenantId = tenantId;
    }

    public void AddDetail(SalaryStructureDetail detail)
    {
        _details.Add(detail);
    }
}

/// <summary>
/// Individual component in a salary structure.
/// Can be fixed amount or formula-based.
/// </summary>
public class SalaryStructureDetail : Entity<Guid>
{
    public Guid SalaryStructureId { get; set; }
    public Guid SalaryComponentId { get; set; }

    /// <summary>Component name (denormalized for display).</summary>
    public string ComponentName { get; set; } = null!;

    /// <summary>Fixed amount (used when Formula is null).</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Formula expression for dynamic calculation.
    /// Uses component abbreviations: e.g., "B * 0.11" means 11% of Basic.
    /// Null means use the fixed Amount.
    /// </summary>
    public string? Formula { get; set; }

    /// <summary>If true, use formula instead of fixed amount.</summary>
    public bool IsFormulaBasedAmount { get; set; }

    /// <summary>Earning or Deduction (inherited from component but stored for quick access).</summary>
    public SalaryComponentType ComponentType { get; set; }

    /// <summary>Whether this component is statistical only (not added to gross/deductions).</summary>
    public bool IsStatisticalComponent { get; set; }

    /// <summary>Condition expression for conditional components (e.g., "base > 5000").</summary>
    public string? Condition { get; set; }

    protected SalaryStructureDetail() { }

    public SalaryStructureDetail(Guid id, Guid salaryStructureId, Guid salaryComponentId,
        string componentName, decimal amount, SalaryComponentType componentType)
        : base(id)
    {
        SalaryStructureId = salaryStructureId;
        SalaryComponentId = salaryComponentId;
        ComponentName = componentName;
        Amount = amount;
        ComponentType = componentType;
    }
}
