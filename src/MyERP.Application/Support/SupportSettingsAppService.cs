using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Support.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Support;

[Authorize(MyERPPermissions.SupportSettings.Default)]
public class SupportSettingsAppService : ApplicationService, ISupportSettingsAppService
{
    private readonly IRepository<SupportSettings, Guid> _repository;

    public SupportSettingsAppService(IRepository<SupportSettings, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<SupportSettingsDto?> GetForCompanyAsync(Guid companyId)
    {
        var query = await _repository.GetQueryableAsync();
        var settings = query.FirstOrDefault(s => s.CompanyId == companyId);
        return settings != null ? ObjectMapper.Map<SupportSettings, SupportSettingsDto>(settings) : null;
    }

    [Authorize(MyERPPermissions.SupportSettings.Edit)]
    public async Task<SupportSettingsDto> SaveAsync(SaveSupportSettingsDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var existing = query.FirstOrDefault(s => s.CompanyId == input.CompanyId);

        if (existing == null)
        {
            existing = new SupportSettings(GuidGenerator.Create(), input.CompanyId, CurrentTenant.Id);
            await _repository.InsertAsync(existing);
        }

        existing.TrackServiceLevelAgreement = input.TrackServiceLevelAgreement;
        existing.AllowResettingServiceLevelAgreement = input.AllowResettingServiceLevelAgreement;
        existing.CloseIssueAfterDays = input.CloseIssueAfterDays;

        await _repository.UpdateAsync(existing);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SupportSettings", existing.Id,
            "Saved", existing.CompanyId,
            "SupportSettings", "", "Saved", CurrentUser.Id,
            $"Support settings updated for company {existing.CompanyId}", CurrentTenant.Id));

        return ObjectMapper.Map<SupportSettings, SupportSettingsDto>(existing);
    }
}
