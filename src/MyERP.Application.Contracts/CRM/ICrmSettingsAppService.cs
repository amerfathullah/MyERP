using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.CRM;

public interface ICrmSettingsAppService : IApplicationService
{
    Task<CrmSettingsDto> GetAsync();
    Task<CrmSettingsDto> UpdateAsync(UpdateCrmSettingsDto input);
}
