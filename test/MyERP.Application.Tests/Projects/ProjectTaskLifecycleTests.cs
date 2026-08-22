using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Projects.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Projects;

/// <summary>
/// Regression coverage for ProjectAppService's Task CRUD + lifecycle methods
/// (CreateTaskAsync/StartTaskAsync/CompleteTaskAsync/CancelTaskAsync/DeleteTaskAsync): fully
/// implemented on the backend (task-dependency-completion validation, project progress rollup) but
/// entirely unreachable from Angular — the project detail page only ever displayed a read-only Tasks
/// table via GetTasksAsync, with no way to create or manage one. Added a "New Task" panel and
/// per-row Start/Complete/Cancel/Delete actions; this test covers the AppService layer, including the
/// project percent-complete rollup that CompleteTaskAsync triggers (see the Delete test below for a
/// rollup discrepancy found but not yet confirmed/fixed).
/// </summary>
public abstract class ProjectTaskLifecycleTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CompleteTaskAsync_UpdatesProjectPercentCompleteTo100()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepository = GetRequiredService<IRepository<Project, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Project Task Test Co"), autoSave: true);
            var project = new Project(Guid.NewGuid(), company.Id, "PROJ-TASK-001", "Task Lifecycle Test Project");
            await projectRepository.InsertAsync(project, autoSave: true);

            var task = await projectAppService.CreateTaskAsync(new CreateProjectTaskDto
            {
                ProjectId = project.Id,
                Subject = "Do the thing",
            });

            var started = await projectAppService.StartTaskAsync(task.Id);
            started.Status.ShouldBe(ProjectTaskStatus.Working);

            var completed = await projectAppService.CompleteTaskAsync(task.Id);
            completed.Status.ShouldBe(ProjectTaskStatus.Completed);
            completed.Progress.ShouldBe(100m);

            var reloadedProject = await projectRepository.GetAsync(project.Id);
            reloadedProject.PercentComplete.ShouldBe(100m);
        });
    }

    [Fact]
    public async Task CancelTaskAsync_SetsStatusCancelled()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepository = GetRequiredService<IRepository<Project, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Project Task Test Co 2"), autoSave: true);
            var project = new Project(Guid.NewGuid(), company.Id, "PROJ-TASK-002", "Task Lifecycle Test Project 2");
            await projectRepository.InsertAsync(project, autoSave: true);

            var task = await projectAppService.CreateTaskAsync(new CreateProjectTaskDto
            {
                ProjectId = project.Id,
                Subject = "Task to cancel",
            });

            var cancelled = await projectAppService.CancelTaskAsync(task.Id);
            cancelled.Status.ShouldBe(ProjectTaskStatus.Cancelled);
        });
    }

    [Fact]
    public async Task DeleteTaskAsync_RemovesTaskRow()
    {
        // NOTE: DeleteTaskAsync also calls UpdateProjectProgress to recompute Project.PercentComplete.
        // While building this coverage, a real discrepancy was found: querying Project.Tasks (the
        // AutoInclude navigation UpdateProjectProgress reads) immediately after the delete — even
        // within DeleteTaskAsync's own single, isolated unit of work — still returns the deleted task,
        // while a direct IRepository<ProjectTask> query in the exact same unit of work correctly
        // excludes it. Confirmed reproducible on this test suite's SQLite provider; NOT yet confirmed
        // against the real PostgreSQL backend, so it is deliberately not asserted here or claimed as
        // fixed — see the migration memory for 2026-08-22 for the follow-up investigation needed
        // before trusting Project.PercentComplete right after a task deletion in production.
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepository = GetRequiredService<IRepository<Project, Guid>>();
            var taskRepository = GetRequiredService<IRepository<ProjectTask, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Project Task Test Co 3"), autoSave: true);
            var project = new Project(Guid.NewGuid(), company.Id, "PROJ-TASK-003", "Task Lifecycle Test Project 3");
            await projectRepository.InsertAsync(project, autoSave: true);

            var task = await projectAppService.CreateTaskAsync(new CreateProjectTaskDto
            {
                ProjectId = project.Id,
                Subject = "Task to delete",
            });

            await projectAppService.DeleteTaskAsync(task.Id);

            (await taskRepository.FindAsync(task.Id)).ShouldBeNull();
        });
    }
}
