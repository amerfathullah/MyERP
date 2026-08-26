using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Communication;

public abstract class CommunicationMediumAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICommunicationMediumAppService _communicationMediumAppService;

    protected CommunicationMediumAppServiceTests()
    {
        _communicationMediumAppService = GetRequiredService<ICommunicationMediumAppService>();
    }

    [Fact]
    public async Task CreateAsync_And_GetListAsync_ShouldWork()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var empGroupId = Guid.NewGuid();
            var created = await _communicationMediumAppService.CreateAsync(new CreateUpdateCommunicationMediumDto
            {
                CommunicationMediumType = CommunicationMediumType.Email,
                CommunicationChannel = "support@myerp.com",
                CatchAllEmployeeGroupId = empGroupId,
                IsDisabled = false,
                Timeslots = new List<CreateUpdateCommunicationMediumTimeslotDto>
                {
                    new()
                    {
                        DayOfWeek = DayOfWeek.Monday,
                        FromTime = new TimeSpan(9, 0, 0),
                        ToTime = new TimeSpan(18, 0, 0),
                        EmployeeGroupId = empGroupId
                    }
                }
            });

            created.Id.ShouldNotBe(Guid.Empty);
            created.CommunicationChannel.ShouldBe("support@myerp.com");
            created.Timeslots.Count.ShouldBe(1);

            var list = await _communicationMediumAppService.GetListAsync(new GetCommunicationMediumListDto { Filter = "support" });
            list.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
            list.Items.ShouldContain(x => x.CommunicationChannel == "support@myerp.com");

            var handlingGroup = await _communicationMediumAppService.GetHandlingEmployeeGroupAsync(
                created.Id,
                DayOfWeek.Monday,
                new TimeSpan(11, 0, 0));
            handlingGroup.ShouldBe(empGroupId);
        });
    }
}
