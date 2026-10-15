using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Assets.Entities;
using MyERP.Core.Entities;
using MyERP.HumanResources.Entities;
using MyERP.Maintenance;
using MyERP.Maintenance.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Assets;

public abstract class AssetMaintenanceTeamAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_ManagerNotMember_ThrowsMaintenanceManagerMustBeMember()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepo = GetRequiredService<IRepository<Employee, Guid>>();
            var teamAppService = GetRequiredService<IAssetMaintenanceTeamAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Team Test Co 1"), autoSave: true);
            var emp1 = await employeeRepo.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-TM-01", "Emp 1"), autoSave: true);
            var emp2 = await employeeRepo.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-TM-02", "Emp 2"), autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                teamAppService.CreateAsync(new CreateUpdateAssetMaintenanceTeamDto
                {
                    CompanyId = company.Id,
                    TeamName = "Test Team Without Manager In Members",
                    MaintenanceManagerId = emp1.Id,
                    Members = new List<AssetMaintenanceTeamMemberDto>
                    {
                        new() { EmployeeId = emp2.Id, MaintenanceRole = "Technician" }
                    }
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.MaintenanceManagerMustBeMember);
        });
    }

    [Fact]
    public async Task CreateAsync_DuplicateMember_ThrowsValidationFailed()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepo = GetRequiredService<IRepository<Employee, Guid>>();
            var teamAppService = GetRequiredService<IAssetMaintenanceTeamAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Team Test Co 2"), autoSave: true);
            var emp1 = await employeeRepo.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-TM-03", "Emp 3"), autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                teamAppService.CreateAsync(new CreateUpdateAssetMaintenanceTeamDto
                {
                    CompanyId = company.Id,
                    TeamName = "Duplicate Members Team",
                    Members = new List<AssetMaintenanceTeamMemberDto>
                    {
                        new() { EmployeeId = emp1.Id, MaintenanceRole = "Role A" },
                        new() { EmployeeId = emp1.Id, MaintenanceRole = "Role B" }
                    }
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task UpdateAsync_RemovingMemberWithAssignedTask_ThrowsCannotRemoveTeamMemberWithTasks()
    {
        // Per ERPNext PR #60036 / commit ba3dfc7989: refuse removing member who has active task
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepo = GetRequiredService<IRepository<Employee, Guid>>();
            var assetRepo = GetRequiredService<IRepository<Asset, Guid>>();
            var maintenanceRepo = GetRequiredService<IRepository<AssetMaintenance, Guid>>();
            var teamAppService = GetRequiredService<IAssetMaintenanceTeamAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Team Test Co 3"), autoSave: true);
            var manager = await employeeRepo.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-MGR-1", "Manager"), autoSave: true);
            var tech = await employeeRepo.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-TCH-1", "Tech"), autoSave: true);

            var teamDto = await teamAppService.CreateAsync(new CreateUpdateAssetMaintenanceTeamDto
            {
                CompanyId = company.Id,
                TeamName = "Operations Team",
                MaintenanceManagerId = manager.Id,
                Members = new List<AssetMaintenanceTeamMemberDto>
                {
                    new() { EmployeeId = manager.Id, MaintenanceRole = "Manager" },
                    new() { EmployeeId = tech.Id, MaintenanceRole = "Technician" }
                }
            });

            var asset = await assetRepo.InsertAsync(new Asset(Guid.NewGuid(), company.Id, "AST-TM-01", "CNC Machine", DateTime.UtcNow, 10000m), autoSave: true);
            var maintenance = new AssetMaintenance(Guid.NewGuid(), company.Id, asset.Id)
            {
                MaintenanceTeamId = teamDto.Id,
                MaintenanceManagerId = manager.Id,
            };
            maintenance.AddTask("Lubrication", MaintenancePeriodicity.Monthly, DateTime.UtcNow, assignToEmployeeId: tech.Id);
            await maintenanceRepo.InsertAsync(maintenance, autoSave: true);

            // Try to remove tech from the team
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                teamAppService.UpdateAsync(teamDto.Id, new CreateUpdateAssetMaintenanceTeamDto
                {
                    CompanyId = company.Id,
                    TeamName = "Operations Team",
                    MaintenanceManagerId = manager.Id,
                    Members = new List<AssetMaintenanceTeamMemberDto>
                    {
                        new() { EmployeeId = manager.Id, MaintenanceRole = "Manager" }
                    }
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.CannotRemoveTeamMemberWithTasks);
        });
    }

    [Fact]
    public async Task UpdateAsync_RemovingMemberWithoutAssignedTask_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var employeeRepo = GetRequiredService<IRepository<Employee, Guid>>();
            var assetRepo = GetRequiredService<IRepository<Asset, Guid>>();
            var maintenanceRepo = GetRequiredService<IRepository<AssetMaintenance, Guid>>();
            var teamAppService = GetRequiredService<IAssetMaintenanceTeamAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Team Test Co 4"), autoSave: true);
            var manager = await employeeRepo.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-MGR-2", "Manager 2"), autoSave: true);
            var techWithTask = await employeeRepo.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-TCH-2", "Tech 2"), autoSave: true);
            var techWithoutTask = await employeeRepo.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-TCH-3", "Tech 3"), autoSave: true);

            var teamDto = await teamAppService.CreateAsync(new CreateUpdateAssetMaintenanceTeamDto
            {
                CompanyId = company.Id,
                TeamName = "Field Support Team",
                MaintenanceManagerId = manager.Id,
                Members = new List<AssetMaintenanceTeamMemberDto>
                {
                    new() { EmployeeId = manager.Id, MaintenanceRole = "Manager" },
                    new() { EmployeeId = techWithTask.Id, MaintenanceRole = "Lead Tech" },
                    new() { EmployeeId = techWithoutTask.Id, MaintenanceRole = "Junior Tech" }
                }
            });

            var asset = await assetRepo.InsertAsync(new Asset(Guid.NewGuid(), company.Id, "AST-TM-02", "Pump", DateTime.UtcNow, 5000m), autoSave: true);
            var maintenance = new AssetMaintenance(Guid.NewGuid(), company.Id, asset.Id)
            {
                MaintenanceTeamId = teamDto.Id,
                MaintenanceManagerId = manager.Id,
            };
            maintenance.AddTask("Inspection", MaintenancePeriodicity.Monthly, DateTime.UtcNow, assignToEmployeeId: techWithTask.Id);
            await maintenanceRepo.InsertAsync(maintenance, autoSave: true);

            // Remove techWithoutTask from the team — should succeed
            var updatedTeam = await teamAppService.UpdateAsync(teamDto.Id, new CreateUpdateAssetMaintenanceTeamDto
            {
                CompanyId = company.Id,
                TeamName = "Field Support Team",
                MaintenanceManagerId = manager.Id,
                Members = new List<AssetMaintenanceTeamMemberDto>
                {
                    new() { EmployeeId = manager.Id, MaintenanceRole = "Manager" },
                    new() { EmployeeId = techWithTask.Id, MaintenanceRole = "Lead Tech" }
                }
            });

            updatedTeam.Members.Count.ShouldBe(2);
            updatedTeam.Members.ShouldNotContain(m => m.EmployeeId == techWithoutTask.Id);
        });
    }
}
