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
/// holiday awareness.
///
/// While writing this coverage, found the algorithm itself deviated from ERPNext's
/// create_schedule_list/validate_schedule_date_for_holiday_list on two points, confirmed against
/// erpnext/maintenance/doctype/maintenance_schedule/maintenance_schedule.py: (1) it shifted a
/// holiday-colliding date FORWARD instead of BACKWARD (the HolidayList entity's own doc comment
/// already said "backward-shift" — the AppService just didn't do it), and (2) it counted the bare
/// start date as visit 1 instead of advancing one interval first. Fixed both to match source; the
/// dates below reflect the corrected algorithm.
/// </summary>
public abstract class MaintenanceScheduleGenerateScheduleTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GenerateScheduleAsync_ShiftsScheduledDateBackwardOffHoliday()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var holidayListRepository = GetRequiredService<IRepository<HolidayList, Guid>>();
            var scheduleRepository = GetRequiredService<IRepository<MaintenanceSchedule, Guid>>();
            var scheduleAppService = GetRequiredService<IMaintenanceScheduleAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Maintenance Schedule Test Co"), autoSave: true);

            var holidayList = new HolidayList(Guid.NewGuid(), company.Id, "MY 2026", 2026);
            holidayList.AddHoliday(new Holiday(Guid.NewGuid(), holidayList.Id, new DateTime(2026, 3, 16), "Test Holiday"));
            await holidayListRepository.InsertAsync(holidayList, autoSave: true);

            var schedule = new MaintenanceSchedule(
                Guid.NewGuid(), company.Id, new DateTime(2026, 3, 2), new DateTime(2026, 3, 30), "Weekly");
            await scheduleRepository.InsertAsync(schedule, autoSave: true);

            await scheduleAppService.GenerateScheduleAsync(schedule.Id);

            var reloaded = await scheduleRepository.GetAsync(schedule.Id);
            var dates = reloaded.Details.OrderBy(d => d.ScheduledDate).Select(d => d.ScheduledDate).ToList();

            // Weekly over a 28-day window = 4 visits at start+7/14/21/28 days (never the bare start date).
            dates.Count.ShouldBe(4);
            dates[0].ShouldBe(new DateTime(2026, 3, 9));
            dates[1].ShouldBe(new DateTime(2026, 3, 15)); // shifted BACKWARD off the 3/16 holiday
            dates[2].ShouldBe(new DateTime(2026, 3, 23));
            dates[3].ShouldBe(new DateTime(2026, 3, 30));
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
