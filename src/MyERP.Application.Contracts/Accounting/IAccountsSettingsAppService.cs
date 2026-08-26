using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IAccountsSettingsAppService : IApplicationService
{
    Task<AccountsSettingsDto> GetAsync();
    Task<AccountsSettingsDto> UpdateAsync(UpdateAccountsSettingsDto input);
}
