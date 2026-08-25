using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.HumanResources.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

public class WorkstationSchedulingServiceTests
{
    private static (WorkstationSchedulingService Service, Workstation Workstation, JobCard OverlappingJc) CreateService(
        int productionCapacity, bool? disableCapacityPlanning)
    {
        var companyId = Guid.NewGuid();
        var workstation = new Workstation(Guid.NewGuid(), companyId, "CNC-1") { ProductionCapacity = productionCapacity };

        var overlappingJc = new JobCard(Guid.NewGuid(), companyId, Guid.NewGuid(), Guid.NewGuid(), 10, 1)
        {
            WorkstationId = workstation.Id,
        };
        overlappingJc.Start(); // WorkInProgress

        var workstationRepo = Substitute.For<IRepository<Workstation, Guid>>();
        workstationRepo.GetAsync(workstation.Id).Returns(workstation);

        var jobCardRepo = Substitute.For<IRepository<JobCard, Guid>>();
        jobCardRepo.GetQueryableAsync().Returns(new List<JobCard> { overlappingJc }.AsQueryable());

        var holidayRepo = Substitute.For<IRepository<HolidayList, Guid>>();

        var settingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();
        if (disableCapacityPlanning.HasValue)
        {
            var settings = new ManufacturingSettings(Guid.NewGuid(), companyId) { DisableCapacityPlanning = disableCapacityPlanning.Value };
            settingsRepo.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ManufacturingSettings, bool>>>(), Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
                .Returns(settings);
        }

        var service = new WorkstationSchedulingService(workstationRepo, jobCardRepo, holidayRepo, settingsRepo);
        return (service, workstation, overlappingJc);
    }

    [Fact]
    public async Task ValidateNoOverlapAsync_CapacityExceeded_Throws()
    {
        var (service, workstation, _) = CreateService(productionCapacity: 1, disableCapacityPlanning: false);

        await Should.ThrowAsync<BusinessException>(() =>
            service.ValidateNoOverlapAsync(workstation.Id, DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
    }

    [Fact]
    public async Task ValidateNoOverlapAsync_DisableCapacityPlanning_SkipsCheckEvenWhenExceeded()
    {
        var (service, workstation, _) = CreateService(productionCapacity: 1, disableCapacityPlanning: true);

        await Should.NotThrowAsync(() =>
            service.ValidateNoOverlapAsync(workstation.Id, DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
    }

    [Fact]
    public async Task ValidateNoOverlapAsync_NoSettingsRow_DefaultsToCapacityPlanningEnabled()
    {
        var (service, workstation, _) = CreateService(productionCapacity: 1, disableCapacityPlanning: null);

        await Should.ThrowAsync<BusinessException>(() =>
            service.ValidateNoOverlapAsync(workstation.Id, DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
    }
}
