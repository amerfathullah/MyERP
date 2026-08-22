using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.HumanResources.Entities;
using MyERP.Maintenance.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Maintenance;

/// <summary>
/// Regression coverage for MaintenanceScheduleAppService.GenerateScheduleAsync: had zero callers
/// anywhere in Angular and zero test coverage. The maintenance-schedule detail page (which lives in
/// the Assets module and creates schedules via a separate, simpler AppService) already auto-generates
/// visit dates on create via its own private GenerateScheduleDetails -- but that path is NOT
/// holiday-aware. This endpoint is the holiday-aware version (per ERPNext gotcha #850) that operates
/// on the same MaintenanceSchedule entity/table, and was entirely unreachable. Added a "Generate
/// Schedule" button (Draft-only, since it wipes existing details) to let a user regenerate with
/// holiday awareness; this test covers the backend it now actually reaches.
/// </summary>
public abstract class MaintenanceScheduleGenerateScheduleTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GenerateScheduleAsync_ShiftsScheduledDateForwardPastHoliday()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var holidayListRepository = GetRequiredService<IRepository<HolidayList, Guid>>();
            var scheduleRepository = GetRequiredService<IRepository<MaintenanceSchedule, Guid>>();
            var scheduleAppService = GetRequiredService<IMaintenanceScheduleAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Maintenance Schedule Test Co"), autoSave: true);

            var holidayList = new HolidayList(Guid.NewGuid(), company.Id, "MY 2026", 2026);
            holidayList.AddHoliday(new Holiday(Guid.NewGuid(), holidayList.Id, new DateTime(2026, 3, 2), "Test Holiday"));
            await holidayListRepository.InsertAsync(holidayList, autoSave: true);

            var schedule = new MaintenanceSchedule(
                Guid.NewGuid(), company.Id, new DateTime(2026, 3, 2), new DateTime(2026, 3, 30), "Weekly");
            await scheduleRepository.InsertAsync(schedule, autoSave: true);

            await scheduleAppService.GenerateScheduleAsync(schedule.Id);

            var reloaded = await scheduleRepository.GetAsync(schedule.Id);
            var dates = reloaded.Details.OrderBy(d => d.ScheduledDate).Select(d => d.ScheduledDate).ToList();

            dates.Count.ShouldBe(4);
            dates[0].ShouldBe(new DateTime(2026, 3, 3)); // shifted off the holiday
            dates[1].ShouldBe(new DateTime(2026, 3, 9));
            dates[2].ShouldBe(new DateTime(2026, 3, 16));
            dates[3].ShouldBe(new DateTime(2026, 3, 23));
        });
    }

    [Fact]
    public async Task GenerateScheduleAsync_CalledTwice_ReplacesRatherThanAppends()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var scheduleRepository = GetRequiredService<IRepository<MaintenanceSchedule, Guid>>();
            var scheduleAppService = GetRequiredService<IMaintenanceScheduleAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Maintenance Schedule Test Co 2"), autoSave: true);

            var schedule = new MaintenanceSchedule(
                Guid.NewGuid(), company.Id, new DateTime(2026, 3, 2), new DateTime(2026, 3, 30), "Weekly");
            await scheduleRepository.InsertAsync(schedule, autoSave: true);

            await scheduleAppService.GenerateScheduleAsync(schedule.Id);
            await scheduleAppService.GenerateScheduleAsync(schedule.Id);

            var reloaded = await scheduleRepository.GetAsync(schedule.Id);
            reloaded.Details.Count.ShouldBe(4);
        });
    }
}
