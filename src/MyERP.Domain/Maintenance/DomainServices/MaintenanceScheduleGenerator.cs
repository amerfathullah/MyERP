using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.HumanResources.Entities;
using MyERP.Maintenance.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Maintenance.DomainServices;

/// <summary>
/// Generates evenly-spaced Maintenance Schedule visit dates with holiday awareness, per ERPNext
/// maintenance_schedule.py's create_schedule_list/validate_schedule_date_for_holiday_list (gotcha
/// #850): each visit advances one interval before being recorded (never the bare start date), and a
/// visit landing on a holiday shifts backward day-by-day, capped at the holiday list's own count.
///
/// Shared by both places that create/regenerate a Maintenance Schedule's details — the Assets
/// module's create-time auto-generation and the standalone Maintenance module's manual "Generate
/// Schedule" action — so both paths always use the same, holiday-aware algorithm rather than two
/// independently-maintained copies.
/// </summary>
public class MaintenanceScheduleGenerator : DomainService
{
    private readonly IRepository<HolidayList, Guid> _holidayListRepository;

    public MaintenanceScheduleGenerator(IRepository<HolidayList, Guid> holidayListRepository)
    {
        _holidayListRepository = holidayListRepository;
    }

    /// <summary>
    /// Clears and regenerates <paramref name="schedule"/>'s visit details. Returns the number of
    /// visits generated.
    /// </summary>
    public async Task<int> GenerateAsync(MaintenanceSchedule schedule)
    {
        schedule.ClearDetails();

        var dateDiff = (schedule.EndDate - schedule.StartDate).Days;
        var daysInPeriod = GetDaysInPeriod(schedule.Periodicity);
        var noOfVisits = daysInPeriod > 0 ? Math.Max(1, dateDiff / daysInPeriod) : 1;
        var interval = dateDiff > 0 ? Math.Max(1, dateDiff / noOfVisits) : 0;

        var holidayLists = await _holidayListRepository.GetListAsync(h => h.CompanyId == schedule.CompanyId, includeDetails: true);

        for (int i = 0; i < noOfVisits; i++)
        {
            // Advance BEFORE recording — visit 1 is start_date + interval, never the bare start date.
            var scheduledDate = schedule.StartDate.AddDays((i + 1) * interval);
            if (scheduledDate > schedule.EndDate)
                scheduledDate = schedule.EndDate;

            var holidayList = holidayLists.FirstOrDefault(h => h.Year == scheduledDate.Year)
                ?? holidayLists.FirstOrDefault(h => h.IsDefault);

            if (holidayList != null)
            {
                // Shift BACKWARD off a holiday, capped at holidays.Count iterations, never before
                // the schedule start.
                var maxIterations = holidayList.Holidays.Count;
                for (int iter = 0; iter < maxIterations && holidayList.IsHoliday(scheduledDate); iter++)
                {
                    scheduledDate = scheduledDate.AddDays(-1);
                    if (scheduledDate <= schedule.StartDate)
                        break;
                }
            }

            schedule.AddDetail(new MaintenanceScheduleDetail(GuidGenerator.Create(), schedule.Id, scheduledDate));
        }

        return noOfVisits;
    }

    public static int GetDaysInPeriod(string periodicity) => periodicity switch
    {
        "Weekly" => 7,
        "Monthly" => 30,
        "Quarterly" => 91,
        "HalfYearly" or "Half Yearly" => 182,
        "Yearly" => 365,
        "TwoYearly" => 730,
        "ThreeYearly" => 1095,
        _ => 30
    };
}
