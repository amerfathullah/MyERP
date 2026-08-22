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
/// project percent-complete rollup that Complete/DeleteTaskAsync trigger (see the Delete test below —
/// that rollup had a real cross-provider staleness bug, confirmed on both SQLite and PostgreSQL and
/// fixed by having UpdateProjectProgress query tasks directly instead of trusting the AutoInclude
/// navigation).
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
    public async Task DeleteTaskAsync_RemovesTaskAndResetsProjectPercentCompleteToZero()
    {
        // Regression test for a confirmed cross-provider bug (SQLite AND real PostgreSQL, verified
        // against a scratch database): UpdateProjectProgress used to reload Project.Tasks via its
        // AutoInclude navigation, which still returned the just-deleted task within DeleteTaskAsync's
        // own single unit of work, even though a direct IRepository<ProjectTask> query in the exact
        // same scope correctly excluded it. Fixed by having UpdateProjectProgress query tasks directly
        // via the task repository and passing that explicit list into Project.UpdateProgress(tasks)
        // instead of the parameterless overload that reads the (unreliable, in this context) navigation.
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
            await projectAppService.CompleteTaskAsync(task.Id);

            await projectAppService.DeleteTaskAsync(task.Id);

            (await taskRepository.FindAsync(task.Id)).ShouldBeNull();

            var reloadedProject = await projectRepository.GetAsync(project.Id);
            reloadedProject.PercentComplete.ShouldBe(0m);
        });
    }
}
