using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Employee Group — logical grouping of employees for shift assignment, leave allocation, and departmental approvals.
/// Maps to ERPNext setup/doctype/employee_group.
/// </summary>
public class EmployeeGroup : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string GroupName { get; set; } = null!;
    public bool IsDisabled { get; set; }

    private readonly List<EmployeeGroupItem> _items = new();
    public IReadOnlyList<EmployeeGroupItem> Items => _items.AsReadOnly();

    protected EmployeeGroup() { }

    public EmployeeGroup(Guid id, Guid companyId, string groupName, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        GroupName = Check.NotNullOrWhiteSpace(groupName, nameof(groupName), EmployeeGroupConsts.MaxGroupNameLength);
        TenantId = tenantId;
    }

    public void AddEmployee(Guid employeeId, string employeeName, string? designation = null)
    {
        if (_items.Any(i => i.EmployeeId == employeeId))
            return;

        _items.Add(new EmployeeGroupItem(Guid.NewGuid(), Id, employeeId, employeeName, designation));
    }

    public void RemoveEmployee(Guid employeeId)
    {
        _items.RemoveAll(i => i.EmployeeId == employeeId);
    }

    public void ClearEmployees()
    {
        _items.Clear();
    }
}

public class EmployeeGroupItem : CreationAuditedEntity<Guid>
{
    public Guid EmployeeGroupId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = null!;
    public string? Designation { get; set; }

    protected EmployeeGroupItem() { }

    public EmployeeGroupItem(Guid id, Guid employeeGroupId, Guid employeeId, string employeeName, string? designation = null)
        : base(id)
    {
        EmployeeGroupId = employeeGroupId;
        EmployeeId = Check.NotDefaultOrNull<Guid>(employeeId, nameof(employeeId));
        EmployeeName = Check.NotNullOrWhiteSpace(employeeName, nameof(employeeName));
        Designation = designation;
    }
}
