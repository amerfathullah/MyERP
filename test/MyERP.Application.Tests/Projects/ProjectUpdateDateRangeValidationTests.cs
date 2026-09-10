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
/// Project.validate() calls validate_from_to_dates on every save, not just creation.
/// CreateAsync already rejected ExpectedEndDate before ExpectedStartDate (and negative
/// EstimatedCost), but UpdateAsync applied both fields with no check at all, letting an edit
/// silently save an inverted date range or a negative estimated cost.
/// </summary>
public abstract class ProjectUpdateDateRangeValidationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task UpdateAsync_EndDateBeforeStartDate_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepository = GetRequiredService<IRepository<Project, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Project Update Guard Co"), autoSave: true);
            var project = new Project(Guid.NewGuid(), company.Id, "PROJ-UPD-001", "Update Guard Project");
            await projectRepository.InsertAsync(project, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                projectAppService.UpdateAsync(project.Id, new UpdateProjectDto
                {
                    ProjectName = project.ProjectName,
                    ExpectedStartDate = new DateTime(2026, 6, 10),
                    ExpectedEndDate = new DateTime(2026, 6, 1),
                }));
        });
    }

    [Fact]
    public async Task UpdateAsync_NegativeEstimatedCost_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepository = GetRequiredService<IRepository<Project, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Project Update Guard Co 2"), autoSave: true);
            var project = new Project(Guid.NewGuid(), company.Id, "PROJ-UPD-002", "Update Guard Project 2");
            await projectRepository.InsertAsync(project, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                projectAppService.UpdateAsync(project.Id, new UpdateProjectDto
                {
                    ProjectName = project.ProjectName,
                    EstimatedCost = -100m,
                }));
        });
    }

    [Fact]
    public async Task UpdateAsync_ValidDateRange_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var projectRepository = GetRequiredService<IRepository<Project, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Project Update Happy Co"), autoSave: true);
            var project = new Project(Guid.NewGuid(), company.Id, "PROJ-UPD-003", "Update Happy Project");
            await projectRepository.InsertAsync(project, autoSave: true);

            var dto = await projectAppService.UpdateAsync(project.Id, new UpdateProjectDto
            {
                ProjectName = project.ProjectName,
                ExpectedStartDate = new DateTime(2026, 6, 1),
                ExpectedEndDate = new DateTime(2026, 6, 10),
                EstimatedCost = 500m,
            });

            dto.Id.ShouldBe(project.Id);
        });
    }
}
