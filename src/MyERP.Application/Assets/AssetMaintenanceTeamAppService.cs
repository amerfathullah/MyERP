using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Maintenance.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
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
        await _repository.InsertAsync(team);
        return (await MapWithEmployeeNamesAsync(new[] { team })).Single();
    }

    [Authorize(MyERPPermissions.AssetMaintenances.Edit)]
    public async Task<AssetMaintenanceTeamDto> UpdateAsync(Guid id, CreateUpdateAssetMaintenanceTeamDto input)
    {
        var team = await _repository.GetAsync(id, includeDetails: true);
        team.TeamName = input.TeamName;
        team.MaintenanceManagerId = input.MaintenanceManagerId;
        team.SetMembers(input.Members.Select(m => (m.EmployeeId, m.MaintenanceRole)));
        await _repository.UpdateAsync(team);
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
