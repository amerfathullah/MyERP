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
/// Parity tests for ERPNext PR #60635 / commits ba5e28ebee & 4013a2c888:
/// - Task type change fetches the task weight unless the weight changes explicitly.
/// - TaskType weight cannot be negative.
/// </summary>
public abstract class ProjectTaskTypeAndWeightTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateTaskAsync_WithTaskType_DefaultsWeightFromTaskType()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepo = GetRequiredService<IRepository<Project, Guid>>();
            var taskTypeAppService = GetRequiredService<ITaskTypeAppService>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Task Type Test Co 1"), autoSave: true);
            var project = await projectRepo.InsertAsync(new Project(Guid.NewGuid(), company.Id, "PROJ-TT-001", "Task Type Test Project 1"), autoSave: true);

            var taskType = await taskTypeAppService.CreateAsync(new CreateUpdateTaskTypeDto
            {
                Name = "Heavy Duty",
                Weight = 3.5m,
            });

            var task = await projectAppService.CreateTaskAsync(new CreateProjectTaskDto
            {
                ProjectId = project.Id,
                Subject = "Heavy task",
                TaskTypeId = taskType.Id,
            });

            task.TaskTypeId.ShouldBe(taskType.Id);
            task.TaskWeight.ShouldBe(3.5m);
        });
    }

    [Fact]
    public async Task CreateTaskAsync_WithTaskType_AndExplicitWeight_UsesExplicitWeight()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepo = GetRequiredService<IRepository<Project, Guid>>();
            var taskTypeAppService = GetRequiredService<ITaskTypeAppService>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Task Type Test Co 2"), autoSave: true);
            var project = await projectRepo.InsertAsync(new Project(Guid.NewGuid(), company.Id, "PROJ-TT-002", "Task Type Test Project 2"), autoSave: true);

            var taskType = await taskTypeAppService.CreateAsync(new CreateUpdateTaskTypeDto
            {
                Name = "Medium Duty",
                Weight = 2m,
            });

            var task = await projectAppService.CreateTaskAsync(new CreateProjectTaskDto
            {
                ProjectId = project.Id,
                Subject = "Custom weight task",
                TaskTypeId = taskType.Id,
                TaskWeight = 5m,
            });

            task.TaskTypeId.ShouldBe(taskType.Id);
            task.TaskWeight.ShouldBe(5m);
        });
    }

    [Fact]
    public async Task UpdateTaskAsync_WhenTaskTypeChangesAlone_UpdatesWeightFromNewType()
    {
        // Per ERPNext PR #60635: changing task type alone fetches weight from new task type
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepo = GetRequiredService<IRepository<Project, Guid>>();
            var taskTypeAppService = GetRequiredService<ITaskTypeAppService>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Task Type Test Co 3"), autoSave: true);
            var project = await projectRepo.InsertAsync(new Project(Guid.NewGuid(), company.Id, "PROJ-TT-003", "Task Type Test Project 3"), autoSave: true);

            var lightType = await taskTypeAppService.CreateAsync(new CreateUpdateTaskTypeDto
            {
                Name = "Light Task Type",
                Weight = 1m,
            });

            var heavyType = await taskTypeAppService.CreateAsync(new CreateUpdateTaskTypeDto
            {
                Name = "Heavy Task Type",
                Weight = 3m,
            });

            var task = await projectAppService.CreateTaskAsync(new CreateProjectTaskDto
            {
                ProjectId = project.Id,
                Subject = "Retyped task",
                TaskTypeId = lightType.Id,
            });

            task.TaskWeight.ShouldBe(1m);

            // Change type alone without specifying weight
            var updated = await projectAppService.UpdateTaskAsync(task.Id, new UpdateProjectTaskDto
            {
                Subject = task.Subject,
                TaskTypeId = heavyType.Id,
            });

            updated.TaskTypeId.ShouldBe(heavyType.Id);
            updated.TaskWeight.ShouldBe(3m);
        });
    }

    [Fact]
    public async Task UpdateTaskAsync_WhenTaskTypeChangesWithExplicitWeightChange_KeepsExplicitWeight()
    {
        // Per ERPNext commit 4013a2c888: explicit weight change overrides task type default
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepo = GetRequiredService<IRepository<Project, Guid>>();
            var taskTypeAppService = GetRequiredService<ITaskTypeAppService>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Task Type Test Co 4"), autoSave: true);
            var project = await projectRepo.InsertAsync(new Project(Guid.NewGuid(), company.Id, "PROJ-TT-004", "Task Type Test Project 4"), autoSave: true);

            var heavyType = await taskTypeAppService.CreateAsync(new CreateUpdateTaskTypeDto
            {
                Name = "Heavy Task Type 2",
                Weight = 3m,
            });

            var lightType = await taskTypeAppService.CreateAsync(new CreateUpdateTaskTypeDto
            {
                Name = "Light Task Type 2",
                Weight = 1m,
            });

            var task = await projectAppService.CreateTaskAsync(new CreateProjectTaskDto
            {
                ProjectId = project.Id,
                Subject = "Retyped task with manual weight",
                TaskTypeId = heavyType.Id,
            });

            task.TaskWeight.ShouldBe(3m);

            // Change type to light, but explicitly override weight to 0
            var updated = await projectAppService.UpdateTaskAsync(task.Id, new UpdateProjectTaskDto
            {
                Subject = task.Subject,
                TaskTypeId = lightType.Id,
                TaskWeight = 0m,
            });

            updated.TaskTypeId.ShouldBe(lightType.Id);
            updated.TaskWeight.ShouldBe(0m);
        });
    }

    [Fact]
    public void TaskType_NegativeWeight_Throws()
    {
        Should.Throw<BusinessException>(() =>
            new TaskType(Guid.NewGuid(), "Negative Type", weight: -2m));

        var valid = new TaskType(Guid.NewGuid(), "Valid Type", weight: 2m);
        Should.Throw<BusinessException>(() =>
            valid.SetWeight(-1m));
    }
}
