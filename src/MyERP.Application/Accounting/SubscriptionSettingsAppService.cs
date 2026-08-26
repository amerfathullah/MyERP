using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.SubscriptionSettings.Default)]
public class SubscriptionSettingsAppService : MyERPAppService, ISubscriptionSettingsAppService
{
    private readonly IRepository<SubscriptionSettings, Guid> _repository;

    public SubscriptionSettingsAppService(IRepository<SubscriptionSettings, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<SubscriptionSettingsDto> GetAsync()
    {
        var settings = (await _repository.GetQueryableAsync()).FirstOrDefault();
        if (settings == null)
        {
            settings = new SubscriptionSettings(
                GuidGenerator.Create(),
                1,
                false,
                true,
                CurrentTenant.Id);
            await _repository.InsertAsync(settings);
        }

        return new SubscriptionSettingsMapper().Map(settings);
    }

    [Authorize(MyERPPermissions.SubscriptionSettings.Edit)]
    public async Task<SubscriptionSettingsDto> UpdateAsync(UpdateSubscriptionSettingsDto input)
    {
        var settings = (await _repository.GetQueryableAsync()).FirstOrDefault();
        if (settings == null)
        {
            settings = new SubscriptionSettings(
                GuidGenerator.Create(),
                input.GracePeriod,
                input.CancelAfterGrace,
                input.Prorate,
                CurrentTenant.Id);
            await _repository.InsertAsync(settings);
        }
        else
        {
            settings.GracePeriod = input.GracePeriod >= 0 ? input.GracePeriod : 1;
            settings.CancelAfterGrace = input.CancelAfterGrace;
            settings.Prorate = input.Prorate;
            await _repository.UpdateAsync(settings);
        }

        return new SubscriptionSettingsMapper().Map(settings);
    }
}
