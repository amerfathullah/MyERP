using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Maintenance.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

[Authorize(MyERPPermissions.AssetMaintenances.Default)]
public class AssetMaintenanceTeamAppService : ApplicationService, IAssetMaintenanceTeamAppService
{
    private readonly IRepository<AssetMaintenanceTeam, Guid> _repository;
    private readonly IRepository<HumanResources.Entities.Employee, Guid> _employeeRepository;

    public AssetMaintenanceTeamAppService(
        IRepository<AssetMaintenanceTeam, Guid> repository,
        IRepository<HumanResources.Entities.Employee, Guid> employeeRepository)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
    }

    public async Task<PagedResultDto<AssetMaintenanceTeamDto>> GetListAsync(MyERP.Shared.CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(t => t.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(t => t.TeamName.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderBy(t => t.TeamName).Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<AssetMaintenanceTeamDto>(totalCount, await MapWithEmployeeNamesAsync(items));
    }

    public async Task<AssetMaintenanceTeamDto> GetAsync(Guid id)
    {
        var team = (await _repository.WithDetailsAsync()).First(t => t.Id == id);
        return (await MapWithEmployeeNamesAsync(new[] { team })).Single();
    }

    [Authorize(MyERPPermissions.AssetMaintenances.Create)]
    public async Task<AssetMaintenanceTeamDto> CreateAsync(CreateUpdateAssetMaintenanceTeamDto input)
    {
        var team = new AssetMaintenanceTeam(GuidGenerator.Create(), input.CompanyId, input.TeamName, CurrentTenant.Id)
        {
            MaintenanceManagerId = input.MaintenanceManagerId,
        };
        team.SetMembers(input.Members.Select(m => (m.EmployeeId, m.MaintenanceRole)));
        team.Validate();
        await _repository.InsertAsync(team);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "AssetMaintenanceTeam", team.Id,
            "Created", team.CompanyId,
            team.TeamName, "Draft", "Active", CurrentUser.Id,
            $"Asset maintenance team '{team.TeamName}' created with {team.Members.Count} members", CurrentTenant.Id));

        return (await MapWithEmployeeNamesAsync(new[] { team })).Single();
    }

    [Authorize(MyERPPermissions.AssetMaintenances.Edit)]
    public async Task<AssetMaintenanceTeamDto> UpdateAsync(Guid id, CreateUpdateAssetMaintenanceTeamDto input)
    {
        var team = await _repository.GetAsync(id, includeDetails: true);

        // Per ERPNext PR #60036 (commit ba3dfc7989): validate removed members have no active assigned tasks
        var currentMemberIds = team.Members.Select(m => m.EmployeeId).ToHashSet();
        var newMemberIds = input.Members.Select(m => m.EmployeeId).ToHashSet();
        var removedMemberIds = currentMemberIds.Except(newMemberIds).ToList();

        if (removedMemberIds.Count > 0)
        {
            var maintenanceRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<AssetMaintenance, Guid>>();
            var maintenancesQuery = await maintenanceRepo.WithDetailsAsync(am => am.Tasks);
            var maintenances = maintenancesQuery
                .Where(am => am.MaintenanceTeamId == team.Id)
                .ToList();

            var assignedRemovedMemberIds = maintenances
                .SelectMany(am => am.Tasks)
                .Where(t => t.AssignToEmployeeId.HasValue && removedMemberIds.Contains(t.AssignToEmployeeId.Value))
                .Select(t => t.AssignToEmployeeId!.Value)
                .Distinct()
                .ToList();

            if (assignedRemovedMemberIds.Count > 0)
            {
                var employeeQuery = await _employeeRepository.GetQueryableAsync();
                var employeeNames = employeeQuery
                    .Where(e => assignedRemovedMemberIds.Contains(e.Id))
                    .Select(e => e.FullName)
                    .ToList();

                var namesDisplay = employeeNames.Count > 0
                    ? string.Join(", ", employeeNames)
                    : string.Join(", ", assignedRemovedMemberIds);

                throw new BusinessException(MyERPDomainErrorCodes.CannotRemoveTeamMemberWithTasks)
                    .WithData("0", namesDisplay)
                    .WithData("detail", $"Reassign the maintenance tasks of {namesDisplay} before removing them from the team.");
            }
        }

        team.TeamName = input.TeamName;
        team.MaintenanceManagerId = input.MaintenanceManagerId;
        team.SetMembers(input.Members.Select(m => (m.EmployeeId, m.MaintenanceRole)));
        team.Validate();
        await _repository.UpdateAsync(team);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "AssetMaintenanceTeam", team.Id,
            "Updated", team.CompanyId,
            team.TeamName, "Active", "Active", CurrentUser.Id,
            $"Asset maintenance team '{team.TeamName}' updated", CurrentTenant.Id));

        return (await MapWithEmployeeNamesAsync(new[] { team })).Single();
    }

    [Authorize(MyERPPermissions.AssetMaintenances.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    private async Task<List<AssetMaintenanceTeamDto>> MapWithEmployeeNamesAsync(
        IEnumerable<AssetMaintenanceTeam> teams)
    {
        var teamList = teams.ToList();
        var employeeIds = teamList.SelectMany(t => t.Members.Select(m => m.EmployeeId)).Distinct().ToList();
        var employeeQuery = await _employeeRepository.GetQueryableAsync();
        var employeeNames = employeeQuery.Where(e => employeeIds.Contains(e.Id))
            .ToList().ToDictionary(e => e.Id, e => e.FullName);

        return teamList.Select(t => new AssetMaintenanceTeamDto
        {
            Id = t.Id,
            CompanyId = t.CompanyId,
            TeamName = t.TeamName,
            MaintenanceManagerId = t.MaintenanceManagerId,
            Members = t.Members.Select(m => new AssetMaintenanceTeamMemberDto
            {
                EmployeeId = m.EmployeeId,
                EmployeeName = employeeNames.GetValueOrDefault(m.EmployeeId),
                MaintenanceRole = m.MaintenanceRole,
            }).ToList(),
        }).ToList();
    }
}
