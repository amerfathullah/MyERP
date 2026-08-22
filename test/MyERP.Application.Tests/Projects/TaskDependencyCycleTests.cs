using System;
using System.Linq;
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
/// Regression coverage for a gap found while surveying Projects DomainServices for unwired
/// methods: TaskDependencyValidationService.ValidateNoCycleAsync had zero callers anywhere —
/// ProjectTask.AddDependency's own doc comment says "Full cycle detection requires
/// TaskDependencyValidationService," but there was no AppService method that let a user add a
/// dependency between two arbitrary existing tasks at all (the only prior AddDependency call site
/// was template instantiation, which mirrors a template's own graph rather than accepting
/// arbitrary user input). Added IProjectAppService.AddTaskDependencyAsync, wired with this check.
/// </summary>
public abstract class TaskDependencyCycleTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task AddTaskDependencyAsync_DirectCycle_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepository = GetRequiredService<IRepository<Project, Guid>>();
            var taskRepository = GetRequiredService<IRepository<ProjectTask, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Task Dep Cycle Test Co"), autoSave: true);
            var project = await projectRepository.InsertAsync(new Project(Guid.NewGuid(), company.Id, "PROJ-DEP-1", "Dep Test Project"), autoSave: true);

            var taskA = await taskRepository.InsertAsync(new ProjectTask(Guid.NewGuid(), project.Id, "TASK-A", "Task A"), autoSave: true);
            var taskB = await taskRepository.InsertAsync(new ProjectTask(Guid.NewGuid(), project.Id, "TASK-B", "Task B"), autoSave: true);

            // A depends on B — fine.
            await projectAppService.AddTaskDependencyAsync(taskA.Id, taskB.Id);

            // B depends on A would close the loop (A→B→A) — must be rejected.
            await Should.ThrowAsync<BusinessException>(
                () => projectAppService.AddTaskDependencyAsync(taskB.Id, taskA.Id));
        });
    }

    [Fact]
    public async Task AddTaskDependencyAsync_TransitiveCycle_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepository = GetRequiredService<IRepository<Project, Guid>>();
            var taskRepository = GetRequiredService<IRepository<ProjectTask, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Task Dep Cycle Test Co 2"), autoSave: true);
            var project = await projectRepository.InsertAsync(new Project(Guid.NewGuid(), company.Id, "PROJ-DEP-2", "Dep Test Project 2"), autoSave: true);

            var taskA = await taskRepository.InsertAsync(new ProjectTask(Guid.NewGuid(), project.Id, "TASK-A2", "Task A2"), autoSave: true);
            var taskB = await taskRepository.InsertAsync(new ProjectTask(Guid.NewGuid(), project.Id, "TASK-B2", "Task B2"), autoSave: true);
            var taskC = await taskRepository.InsertAsync(new ProjectTask(Guid.NewGuid(), project.Id, "TASK-C2", "Task C2"), autoSave: true);

            // A -> B -> C chain — fine.
            await projectAppService.AddTaskDependencyAsync(taskA.Id, taskB.Id);
            await projectAppService.AddTaskDependencyAsync(taskB.Id, taskC.Id);

            // C -> A would close a 3-node loop (A→B→C→A) — must be rejected even though C and A
            // aren't directly linked yet.
            await Should.ThrowAsync<BusinessException>(
                () => projectAppService.AddTaskDependencyAsync(taskC.Id, taskA.Id));
        });
    }

    [Fact]
    public async Task AddTaskDependencyAsync_ValidDependency_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepository = GetRequiredService<IRepository<Project, Guid>>();
            var taskRepository = GetRequiredService<IRepository<ProjectTask, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Task Dep Cycle Test Co 3"), autoSave: true);
            var project = await projectRepository.InsertAsync(new Project(Guid.NewGuid(), company.Id, "PROJ-DEP-3", "Dep Test Project 3"), autoSave: true);

            var taskA = await taskRepository.InsertAsync(new ProjectTask(Guid.NewGuid(), project.Id, "TASK-A3", "Task A3"), autoSave: true);
            var taskB = await taskRepository.InsertAsync(new ProjectTask(Guid.NewGuid(), project.Id, "TASK-B3", "Task B3"), autoSave: true);

            var result = await projectAppService.AddTaskDependencyAsync(taskA.Id, taskB.Id);
            result.Id.ShouldBe(taskA.Id);

            var reloaded = await taskRepository.GetAsync(taskA.Id);
            reloaded.Dependencies.Count.ShouldBe(1);
            reloaded.Dependencies.Single().DependsOnTaskId.ShouldBe(taskB.Id);
        });
    }
}
