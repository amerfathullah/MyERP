using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface ISubscriptionSettingsAppService : IApplicationService
{
    Task<SubscriptionSettingsDto> GetAsync();
    Task<SubscriptionSettingsDto> UpdateAsync(UpdateSubscriptionSettingsDto input);
}
