using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Defines a shift timing and rules.
/// Maps to ERPNext Shift Type.
/// </summary>
public class ShiftType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = null!;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public Guid? HolidayListId { get; set; }

    protected ShiftType() { }

    public ShiftType(Guid id, Guid companyId, string name, TimeSpan startTime, TimeSpan endTime, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 100);
        StartTime = startTime;
        EndTime = endTime;
        TenantId = tenantId;
    }
}
