using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

[Authorize(MyERPPermissions.Leads.Default)]
public class EmailCampaignAppService : ApplicationService, IEmailCampaignAppService
{
    private readonly IRepository<EmailCampaign, Guid> _repository;
    private readonly IRepository<Campaign, Guid> _campaignRepository;
    private readonly IRepository<Lead, Guid> _leadRepository;
    private readonly IRepository<MyERP.Core.Entities.Contact, Guid> _contactRepository;

    public EmailCampaignAppService(
        IRepository<EmailCampaign, Guid> repository,
        IRepository<Campaign, Guid> campaignRepository,
        IRepository<Lead, Guid> leadRepository,
        IRepository<MyERP.Core.Entities.Contact, Guid> contactRepository)
    {
        _repository = repository;
        _campaignRepository = campaignRepository;
        _leadRepository = leadRepository;
        _contactRepository = contactRepository;
    }

    public async Task<EmailCampaignDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<EmailCampaignDto>> GetListAsync(GetEmailCampaignListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CampaignId.HasValue)
            query = query.Where(e => e.CampaignId == input.CampaignId.Value);
        if (input.Status.HasValue)
            query = query.Where(e => e.Status == input.Status.Value);

        var totalCount = query.Count();
        var items = query.OrderByDescending(e => e.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<EmailCampaignDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.Leads.Create)]
    public async Task<EmailCampaignDto> CreateAsync(CreateEmailCampaignDto input)
    {
        var campaign = (await _campaignRepository.WithDetailsAsync()).First(c => c.Id == input.CampaignId);

        await ValidateRecipientEmailAsync(input.EmailCampaignFor, input.RecipientId);

        var activeQuery = await _repository.GetQueryableAsync();
        var hasActive = activeQuery.Any(e => e.RecipientId == input.RecipientId
            && e.Status != EmailCampaignStatus.Completed && e.Status != EmailCampaignStatus.Unsubscribed);
        if (hasActive)
            throw new BusinessException(MyERPDomainErrorCodes.EmailCampaignDuplicateActive);

        var entity = new EmailCampaign(GuidGenerator.Create(), input.CampaignId, input.EmailCampaignFor,
            input.RecipientId, input.StartDate, campaign.MaxSendAfterDays(), CurrentTenant.Id)
        {
            SenderId = input.SenderId,
        };

        await _repository.InsertAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "EmailCampaign", entity.Id,
            "Created", Guid.Empty,
            entity.EmailCampaignFor.ToString(), "Draft", "Active",
            CurrentUser.Id,
            $"Email campaign created for {entity.EmailCampaignFor} (Recipient: {entity.RecipientId})", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<EmailCampaignDto> UnsubscribeAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Unsubscribe();
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "EmailCampaign", entity.Id,
            "Unsubscribed", Guid.Empty,
            entity.EmailCampaignFor.ToString(), "Active", "Unsubscribed",
            CurrentUser.Id,
            $"Recipient {entity.RecipientId} unsubscribed from email campaign", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private async Task ValidateRecipientEmailAsync(EmailCampaignFor campaignFor, Guid recipientId)
    {
        if (campaignFor == EmailCampaignFor.Lead)
        {
            var lead = await _leadRepository.FindAsync(recipientId);
            if (lead == null || string.IsNullOrWhiteSpace(lead.Email))
            {
                var name = lead?.GetFullName() ?? recipientId.ToString();
                throw new BusinessException(MyERPDomainErrorCodes.EmailCampaignRecipientMissingEmail)
                    .WithData("recipientType", "Lead")
                    .WithData("recipientName", name);
            }
        }
        else if (campaignFor == EmailCampaignFor.Contact)
        {
            var contact = await _contactRepository.FindAsync(recipientId);
            if (contact == null || string.IsNullOrWhiteSpace(contact.Email))
            {
                var name = contact?.FullName ?? recipientId.ToString();
                throw new BusinessException(MyERPDomainErrorCodes.EmailCampaignRecipientMissingEmail)
                    .WithData("recipientType", "Contact")
                    .WithData("recipientName", name);
            }
        }
    }

    private static EmailCampaignDto MapToDto(EmailCampaign e) => new()
    {
        Id = e.Id,
        CampaignId = e.CampaignId,
        EmailCampaignFor = e.EmailCampaignFor,
        RecipientId = e.RecipientId,
        SenderId = e.SenderId,
        StartDate = e.StartDate,
        EndDate = e.EndDate,
        Status = e.Status,
        CreationTime = e.CreationTime,
        LastModificationTime = e.LastModificationTime,
    };
}
