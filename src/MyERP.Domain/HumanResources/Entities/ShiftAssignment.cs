using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

public enum ShiftAssignmentStatus
{
    Active,
    Inactive
}

/// <summary>
/// Assigns a shift to an employee for a specific period.
/// Maps to ERPNext Shift Assignment.
/// </summary>
public class ShiftAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid EmployeeId { get; set; }
    public Guid ShiftTypeId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public ShiftAssignmentStatus Status { get; set; } = ShiftAssignmentStatus.Active;

    protected ShiftAssignment() { }

    public ShiftAssignment(Guid id, Guid companyId, Guid employeeId, Guid shiftTypeId, DateTime startDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        EmployeeId = employeeId;
        ShiftTypeId = shiftTypeId;
        StartDate = startDate.Date;
        TenantId = tenantId;
    }
}
