using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Settings;

public interface IErpSettingsAppService : IApplicationService
{
    Task<Dictionary<string, string>> GetGroupAsync(string group);
    Task UpdateAsync(Dictionary<string, string> settings);
    Task<string> GetAsync(string name);
    Task SetAsync(string name, string value);
}
