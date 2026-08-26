using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Utilities;

public interface IVideoSettingsAppService : IApplicationService
{
    Task<VideoSettingsDto> GetAsync();
    Task<VideoSettingsDto> UpdateAsync(UpdateVideoSettingsDto input);
}
