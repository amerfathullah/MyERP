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
/// Regression coverage for a real gap found via ERPNext validate() parity: ERPNext's
/// Task.validate_progress rejects a Progress % greater than 100. MyERP's UpdateTaskAsync applied
/// input.Progress directly to the entity with no upper-bound check at all.
/// </summary>
public abstract class ProjectTaskProgressValidationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task UpdateTaskAsync_ProgressOver100_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepository = GetRequiredService<IRepository<Project, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Task Progress Guard Co"), autoSave: true);
            var project = new Project(Guid.NewGuid(), company.Id, "PROJ-PROG-001", "Task Progress Guard Project");
            await projectRepository.InsertAsync(project, autoSave: true);

            var task = await projectAppService.CreateTaskAsync(new CreateProjectTaskDto
            {
                ProjectId = project.Id,
                Subject = "Progress guard task",
            });

            await Should.ThrowAsync<BusinessException>(() =>
                projectAppService.UpdateTaskAsync(task.Id, new UpdateProjectTaskDto
                {
                    Subject = task.Subject,
                    Progress = 150m,
                }));
        });
    }

    [Fact]
    public async Task UpdateTaskAsync_ProgressWithinRange_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepository = GetRequiredService<IRepository<Project, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Task Progress Happy Co"), autoSave: true);
            var project = new Project(Guid.NewGuid(), company.Id, "PROJ-PROG-002", "Task Progress Happy Project");
            await projectRepository.InsertAsync(project, autoSave: true);

            var task = await projectAppService.CreateTaskAsync(new CreateProjectTaskDto
            {
                ProjectId = project.Id,
                Subject = "Progress happy task",
            });

            var updated = await projectAppService.UpdateTaskAsync(task.Id, new UpdateProjectTaskDto
            {
                Subject = task.Subject,
                Progress = 50m,
            });

            updated.Progress.ShouldBe(50m);
        });
    }
}
