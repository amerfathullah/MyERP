using System;
using MyERP.Support.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Support;

/// <summary>
/// Unit tests for ServiceLevelAgreement priority rules (Gotcha #827):
/// 1. ResponseTimeHours must be > 0.
/// 2. If ApplyOnResolution is enabled, ResolutionTimeHours must be >= ResponseTimeHours.
/// </summary>
public class ServiceLevelAgreementValidationTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void SLA_AddPriority_ValidTargets_Succeeds()
    {
        var sla = new ServiceLevelAgreement(Guid.NewGuid(), _companyId, "Standard SLA", 24, 4);
        var priority = new ServiceLevelPriority(Guid.NewGuid(), sla.Id, "High", 2m, 8m);

        sla.AddPriority(priority);

        Assert.Single(sla.Priorities);
    }

    [Fact]
    public void SLA_AddPriority_ZeroResponseTime_ThrowsValidationException()
    {
        var sla = new ServiceLevelAgreement(Guid.NewGuid(), _companyId, "Standard SLA", 24, 4);
        var priority = new ServiceLevelPriority(Guid.NewGuid(), sla.Id, "Urgent", 0m, 8m);

        var ex = Assert.Throws<BusinessException>(() => sla.AddPriority(priority));
        Assert.Contains("must be greater than zero", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void SLA_AddPriority_ResolutionTimeLessThanResponseTime_ThrowsValidationException()
    {
        var sla = new ServiceLevelAgreement(Guid.NewGuid(), _companyId, "Standard SLA", 24, 4)
        {
            ApplyOnResolution = true
        };
        var priority = new ServiceLevelPriority(Guid.NewGuid(), sla.Id, "Critical", 8m, 4m); // Resolution 4h < Response 8h

        var ex = Assert.Throws<BusinessException>(() => sla.AddPriority(priority));
        Assert.Contains("must be greater than or equal to Response Time", ex.Data["detail"]?.ToString());
    }
}
