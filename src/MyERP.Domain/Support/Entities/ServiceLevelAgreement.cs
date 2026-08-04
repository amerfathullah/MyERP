using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Volo.Abp;

namespace MyERP.Support.Entities;

public class ServiceLevelAgreement : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = null!;
    public Guid? CustomerGroupId { get; set; }
    public Guid? HolidayListId { get; set; }
    public int ResolutionTimeHours { get; set; }
    public int ResponseTimeHours { get; set; }

    protected ServiceLevelAgreement() { }

    public ServiceLevelAgreement(Guid id, Guid companyId, string name, int resolutionTimeHours, int responseTimeHours, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 100);
        ResolutionTimeHours = resolutionTimeHours;
        ResponseTimeHours = responseTimeHours;
        TenantId = tenantId;
    }
}
