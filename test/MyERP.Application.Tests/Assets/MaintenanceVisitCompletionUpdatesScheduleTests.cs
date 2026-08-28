using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Assets;

/// <summary>
/// Regression coverage: CompleteVisitAsync used to mark the MaintenanceVisit as Completed without
/// ever touching the linked MaintenanceSchedule's MaintenanceScheduleDetail rows. IsCompleted/ActualDate
/// were set nowhere in the codebase, so a schedule's progress %, "next due" lookup, and the nightly
/// reminder job (which only nags on !IsCompleted) could never reflect completed work — every visit
/// ever done still counted as perpetually due. Fixed by marking the schedule's oldest outstanding
/// detail as completed when its visit is completed.
/// </summary>
public abstract class MaintenanceVisitCompletionUpdatesScheduleTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CompleteVisitAsync_MarksOldestOutstandingScheduleDetailCompleted()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var maintenanceAppService = GetRequiredService<IMaintenanceAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Visit Completion Test Co"), autoSave: true);

            var schedule = await maintenanceAppService.CreateScheduleAsync(new CreateMaintenanceScheduleDto
            {
                CompanyId = company.Id,
                StartDate = new DateTime(2026, 3, 2),
                EndDate = new DateTime(2026, 3, 30),
                Periodicity = "Weekly",
            });

            schedule.Details.Length.ShouldBeGreaterThan(0);
            var oldestDetail = schedule.Details.OrderBy(d => d.ScheduledDate).First();
            oldestDetail.IsCompleted.ShouldBeFalse();

            var visit = await maintenanceAppService.CreateVisitAsync(new CreateMaintenanceVisitDto
            {
                CompanyId = company.Id,
                VisitDate = oldestDetail.ScheduledDate,
                MaintenanceType = "Scheduled",
                MaintenanceScheduleId = schedule.Id,
            });

            await maintenanceAppService.CompleteVisitAsync(visit.Id);

            var updatedSchedule = await maintenanceAppService.GetScheduleAsync(schedule.Id);
            var updatedOldestDetail = updatedSchedule.Details.OrderBy(d => d.ScheduledDate).First();
            updatedOldestDetail.Id.ShouldBe(oldestDetail.Id);
            updatedOldestDetail.IsCompleted.ShouldBeTrue();
            updatedOldestDetail.ActualDate.ShouldBe(oldestDetail.ScheduledDate);

            // The next-oldest detail (if any) must remain untouched.
            if (updatedSchedule.Details.Length > 1)
            {
                updatedSchedule.Details.OrderBy(d => d.ScheduledDate).Skip(1).First().IsCompleted.ShouldBeFalse();
            }
        });
    }
}
