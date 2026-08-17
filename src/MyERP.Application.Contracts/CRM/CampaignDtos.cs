using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.CRM;

public class CampaignEmailScheduleDto
{
    public Guid Id { get; set; }
    public Guid EmailTemplateId { get; set; }
    public int SendAfterDays { get; set; }
}

public class CampaignDto : AuditedEntityDto<Guid>
{
    public string CampaignName { get; set; } = null!;
    public string? Description { get; set; }
    public List<CampaignEmailScheduleDto> EmailSchedules { get; set; } = new();
}

public class CreateCampaignEmailScheduleDto
{
    [Required] public Guid EmailTemplateId { get; set; }
    public int SendAfterDays { get; set; }
}

public class CreateUpdateCampaignDto
{
    [Required][StringLength(CampaignConsts.MaxCampaignNameLength)] public string CampaignName { get; set; } = null!;
    [StringLength(CampaignConsts.MaxDescriptionLength)] public string? Description { get; set; }
    public List<CreateCampaignEmailScheduleDto> EmailSchedules { get; set; } = new();
}

public class GetCampaignListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}

public interface ICampaignAppService : IApplicationService
{
    Task<CampaignDto> GetAsync(Guid id);
    Task<PagedResultDto<CampaignDto>> GetListAsync(GetCampaignListDto input);
    Task<CampaignDto> CreateAsync(CreateUpdateCampaignDto input);
    Task<CampaignDto> UpdateAsync(Guid id, CreateUpdateCampaignDto input);
    Task DeleteAsync(Guid id);
}
