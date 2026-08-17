using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Assets;

public class AssetMaintenanceTeamMemberDto
{
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? MaintenanceRole { get; set; }
}

public class AssetMaintenanceTeamDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string TeamName { get; set; } = null!;
    public Guid? MaintenanceManagerId { get; set; }
    public List<AssetMaintenanceTeamMemberDto> Members { get; set; } = new();
}

public class CreateUpdateAssetMaintenanceTeamDto
{
    public Guid CompanyId { get; set; }
    public string TeamName { get; set; } = null!;
    public Guid? MaintenanceManagerId { get; set; }
    public List<AssetMaintenanceTeamMemberDto> Members { get; set; } = new();
}

public interface IAssetMaintenanceTeamAppService : IApplicationService
{
    Task<PagedResultDto<AssetMaintenanceTeamDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<AssetMaintenanceTeamDto> GetAsync(Guid id);
    Task<AssetMaintenanceTeamDto> CreateAsync(CreateUpdateAssetMaintenanceTeamDto input);
    Task<AssetMaintenanceTeamDto> UpdateAsync(Guid id, CreateUpdateAssetMaintenanceTeamDto input);
    Task DeleteAsync(Guid id);
}
