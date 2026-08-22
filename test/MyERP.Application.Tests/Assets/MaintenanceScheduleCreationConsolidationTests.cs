using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.HumanResources.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Assets;

/// <summary>
/// Regression coverage for consolidating maintenance-schedule generation: the Assets module's
/// MaintenanceAppService.CreateScheduleAsync used to auto-generate visit dates via its own private,
/// simpler, NOT-holiday-aware algorithm (fixed month interval starting on the bare start date),
/// independent of the Maintenance module's holiday-aware MaintenanceScheduleAppService.GenerateScheduleAsync
/// even though both operate on the exact same MaintenanceSchedule/MaintenanceScheduleDetail entities.
/// Both now share MaintenanceScheduleGenerator (a domain service), so schedules created via either
/// module get the same, correct, holiday-aware dates from the start.
/// </summary>
public abstract class MaintenanceScheduleCreationConsolidationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateScheduleAsync_GeneratesHolidayAwareDetailsOnCreate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var holidayListRepository = GetRequiredService<IRepository<HolidayList, Guid>>();
            var maintenanceAppService = GetRequiredService<IMaintenanceAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Schedule Consolidation Test Co"), autoSave: true);

            var holidayList = new HolidayList(Guid.NewGuid(), company.Id, "MY 2026", 2026);
            holidayList.AddHoliday(new Holiday(Guid.NewGuid(), holidayList.Id, new DateTime(2026, 3, 16), "Test Holiday"));
            await holidayListRepository.InsertAsync(holidayList, autoSave: true);

            var created = await maintenanceAppService.CreateScheduleAsync(new CreateMaintenanceScheduleDto
            {
                CompanyId = company.Id,
                StartDate = new DateTime(2026, 3, 2),
                EndDate = new DateTime(2026, 3, 30),
                Periodicity = "Weekly",
            });

            var dates = created.Details.OrderBy(d => d.ScheduledDate).Select(d => d.ScheduledDate).ToList();

            // Same expectations as the Maintenance module's own GenerateScheduleAsync test:
            // 4 visits at start+7/14/21/28 days (never the bare start date), with the 3/16 holiday
            // shifting that visit back to 3/15.
            dates.Count.ShouldBe(4);
            dates[0].ShouldBe(new DateTime(2026, 3, 9));
            dates[1].ShouldBe(new DateTime(2026, 3, 15));
            dates[2].ShouldBe(new DateTime(2026, 3, 23));
            dates[3].ShouldBe(new DateTime(2026, 3, 30));
        });
    }
}
