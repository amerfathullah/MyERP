using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Projects.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Projects;

/// <summary>
/// EF Core integration tests for Project Summary and Delayed Tasks Summary reports
/// per ERPNext projects/report/project_summary and delayed_tasks_summary.
/// </summary>
public abstract class ProjectSummaryReportTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ProjectSummaryAndDelayedTasksSummary_ComputeTaskMetricsAndScheduleDelays()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var typeRepo = GetRequiredService<IRepository<ProjectType, Guid>>();
            var projRepo = GetRequiredService<IRepository<Project, Guid>>();
            var taskRepo = GetRequiredService<IRepository<ProjectTask, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Project Report Co"), autoSave: true);
            var projType = await typeRepo.InsertAsync(
                new ProjectType(Guid.NewGuid(), "Implementation"), autoSave: true);

            var project = new Project(Guid.NewGuid(), company.Id, "PROJ-RPT-01", "ERP Rollout")
            {
                ProjectTypeId = projType.Id,
                PercentCompleteMethod = PercentCompleteMethod.Manual,
                ExpectedStartDate = new DateTime(2026, 10, 1),
                ExpectedEndDate = new DateTime(2026, 12, 31),
            };
            project.SetPercentComplete(50m);
            await projRepo.InsertAsync(project, autoSave: true);

            // Task 1: Completed late -> ActualEndDate (Nov 15) vs ExpectedEndDate (Nov 10) => Delay = +5 days
            var lateCompletedTask = new ProjectTask(Guid.NewGuid(), project.Id, "TASK-001", "Data Migration")
            {
                Priority = ProjectPriority.High,
                ExpectedStartDate = new DateTime(2026, 11, 1),
                ExpectedEndDate = new DateTime(2026, 11, 10),
                Progress = 100m,
            };
            lateCompletedTask.SetStatus(ProjectTaskStatus.Completed);
            lateCompletedTask.ActualEndDate = new DateTime(2026, 11, 15);
            await taskRepo.InsertAsync(lateCompletedTask, autoSave: true);

            // Task 2: Open and already past ExpectedEndDate (Nov 1) as of Nov 18 => Delay = +17 days (and counted Overdue)
            var overdueTask = new ProjectTask(Guid.NewGuid(), project.Id, "TASK-002", "UAT Signoff")
            {
                Priority = ProjectPriority.Urgent,
                ExpectedStartDate = new DateTime(2026, 10, 20),
                ExpectedEndDate = new DateTime(2026, 11, 1),
                Progress = 40m,
            };
            await taskRepo.InsertAsync(overdueTask, autoSave: true);

            // Task 3: Cancelled -> excluded from Delayed Tasks Summary
            var cancelledTask = new ProjectTask(Guid.NewGuid(), project.Id, "TASK-003", "Dropped Scope")
            {
                ExpectedEndDate = new DateTime(2026, 10, 25),
            };
            cancelledTask.SetStatus(ProjectTaskStatus.Cancelled);
            await taskRepo.InsertAsync(cancelledTask, autoSave: true);

            var summary = await projectAppService.GetProjectSummaryReportAsync(new ProjectSummaryRequestDto
            {
                CompanyId = company.Id,
                Status = ProjectStatus.Open,
            });

            summary.TotalProjects.ShouldBe(1);
            summary.OpenProjects.ShouldBe(1);
            summary.TotalTasks.ShouldBe(3);
            summary.CompletedTasks.ShouldBe(1);
            summary.OverdueTasks.ShouldBe(1);
            summary.Rows[0].ProjectTypeName.ShouldBe("Implementation");
            summary.Rows[0].PercentComplete.ShouldBe(50m);

            await Should.ThrowAsync<BusinessException>(() =>
                projectAppService.GetDelayedTasksSummaryAsync(new DelayedTasksSummaryRequestDto
                {
                    CompanyId = company.Id,
                    FromDate = new DateTime(2026, 12, 1),
                    ToDate = new DateTime(2026, 11, 1),
                }));

            var delayedReport = await projectAppService.GetDelayedTasksSummaryAsync(new DelayedTasksSummaryRequestDto
            {
                CompanyId = company.Id,
                AsOfDate = new DateTime(2026, 11, 18),
            });

            delayedReport.TotalTasks.ShouldBe(2);
            delayedReport.DelayedCount.ShouldBe(2);
            delayedReport.OnTrackCount.ShouldBe(0);
            // Sorted descending by DelayInDays: TASK-002 (+17) then TASK-001 (+5)
            delayedReport.Rows[0].TaskNumber.ShouldBe("TASK-002");
            delayedReport.Rows[0].DelayInDays.ShouldBe(17);
            delayedReport.Rows[1].TaskNumber.ShouldBe("TASK-001");
            delayedReport.Rows[1].DelayInDays.ShouldBe(5);
        });
    }
}
