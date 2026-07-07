using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Employee master data.
/// Maps to ERPNext setup/doctype/employee.
/// Sensitive fields (IC, bank, salary) require PDPA-level access control.
/// </summary>
public class Employee : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }

    public string EmployeeId { get; private set; } = null!;
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string FullName => string.IsNullOrEmpty(LastName) ? FirstName : $"{FirstName} {LastName}";

    public DateTime? DateOfBirth { get; set; }
    public DateTime? DateOfJoining { get; set; }
    public DateTime? DateOfResignation { get; set; }

    // PDPA-sensitive fields
    public string? IcNumber { get; set; }
    public string? PassportNumber { get; set; }
    public CitizenshipType Citizenship { get; set; } = CitizenshipType.Malaysian;

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    // Employment
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public EmploymentStatus Status { get; set; } = EmploymentStatus.Active;

    // Bank (PDPA-sensitive)
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }

    // Statutory numbers (Malaysia)
    public string? EpfNumber { get; set; }
    public string? SocsoNumber { get; set; }
    public string? TaxNumber { get; set; }  // PCB/MTD income tax number

    // Salary (PDPA-sensitive)
    public decimal? BasicSalary { get; set; }

    protected Employee() { }

    public Employee(Guid id, Guid companyId, string employeeId, string firstName, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        SetEmployeeId(employeeId);
        FirstName = Check.NotNullOrWhiteSpace(firstName, nameof(firstName), EmployeeConsts.MaxNameLength);
        TenantId = tenantId;
    }

    public void SetEmployeeId(string employeeId)
    {
        EmployeeId = Check.NotNullOrWhiteSpace(employeeId, nameof(employeeId), EmployeeConsts.MaxEmployeeIdLength);
    }
}
