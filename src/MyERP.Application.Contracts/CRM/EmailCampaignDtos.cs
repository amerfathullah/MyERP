using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.CRM;

public class EmailCampaignDto : AuditedEntityDto<Guid>
{
    public Guid CampaignId { get; set; }
    public EmailCampaignFor EmailCampaignFor { get; set; }
    public Guid RecipientId { get; set; }
    public Guid? SenderId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public EmailCampaignStatus Status { get; set; }
}

public class CreateEmailCampaignDto
{
    [Required] public Guid CampaignId { get; set; }
    public EmailCampaignFor EmailCampaignFor { get; set; }
    [Required] public Guid RecipientId { get; set; }
    public Guid? SenderId { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
}

public class GetEmailCampaignListDto : PagedAndSortedResultRequestDto
{
    public Guid? CampaignId { get; set; }
    public EmailCampaignStatus? Status { get; set; }
}

public interface IEmailCampaignAppService : IApplicationService
{
    Task<EmailCampaignDto> GetAsync(Guid id);
    Task<PagedResultDto<EmailCampaignDto>> GetListAsync(GetEmailCampaignListDto input);
    Task<EmailCampaignDto> CreateAsync(CreateEmailCampaignDto input);
    Task<EmailCampaignDto> UnsubscribeAsync(Guid id);
    Task DeleteAsync(Guid id);
}
