using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

[Authorize(MyERPPermissions.CrmSettings.Default)]
public class CrmSettingsAppService : MyERPAppService, ICrmSettingsAppService
{
    private readonly IRepository<CrmSettings, Guid> _repository;

    public CrmSettingsAppService(IRepository<CrmSettings, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CrmSettingsDto> GetAsync()
    {
        var settings = (await _repository.GetQueryableAsync()).FirstOrDefault();
        if (settings == null)
        {
            settings = new CrmSettings(
                GuidGenerator.Create(),
                "Campaign Name",
                false,
                true,
                15,
                false,
                30,
                false,
                false,
                CurrentTenant.Id);
            await _repository.InsertAsync(settings);
        }

        return new CrmSettingsMapper().Map(settings);
    }

    [Authorize(MyERPPermissions.CrmSettings.Edit)]
    public async Task<CrmSettingsDto> UpdateAsync(UpdateCrmSettingsDto input)
    {
        var settings = (await _repository.GetQueryableAsync()).FirstOrDefault();
        if (settings == null)
        {
            settings = new CrmSettings(
                GuidGenerator.Create(),
                input.CampaignNamingBy,
                input.AllowLeadDuplicationBasedOnEmails,
                input.AutoCreationOfContact,
                input.CloseOpportunityAfterDays,
                input.EnableOpportunityCreationFromContactUs,
                input.DefaultQuotationValidityDays,
                input.CarryForwardCommunicationAndComments,
                input.UpdateTimestampOnNewCommunication,
                CurrentTenant.Id);
            await _repository.InsertAsync(settings);
        }
        else
        {
            settings.CampaignNamingBy = input.CampaignNamingBy;
            settings.AllowLeadDuplicationBasedOnEmails = input.AllowLeadDuplicationBasedOnEmails;
            settings.AutoCreationOfContact = input.AutoCreationOfContact;
            settings.CloseOpportunityAfterDays = input.CloseOpportunityAfterDays;
            settings.EnableOpportunityCreationFromContactUs = input.EnableOpportunityCreationFromContactUs;
            settings.DefaultQuotationValidityDays = input.DefaultQuotationValidityDays;
            settings.CarryForwardCommunicationAndComments = input.CarryForwardCommunicationAndComments;
            settings.UpdateTimestampOnNewCommunication = input.UpdateTimestampOnNewCommunication;
            await _repository.UpdateAsync(settings);
        }

        return new CrmSettingsMapper().Map(settings);
    }
}
