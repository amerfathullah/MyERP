using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Utilities.Entities;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Utilities;

[Authorize(MyERPPermissions.VideoSettings.Default)]
public class VideoSettingsAppService : MyERPAppService, IVideoSettingsAppService
{
    private readonly IRepository<VideoSettings, Guid> _repository;

    public VideoSettingsAppService(IRepository<VideoSettings, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<VideoSettingsDto> GetAsync()
    {
        var settings = (await _repository.GetQueryableAsync()).FirstOrDefault();
        if (settings == null)
        {
            settings = new VideoSettings(
                GuidGenerator.Create(),
                false,
                null,
                60,
                CurrentTenant.Id);
            await _repository.InsertAsync(settings);
        }

        return new VideoSettingsMapper().Map(settings);
    }

    [Authorize(MyERPPermissions.VideoSettings.Edit)]
    public async Task<VideoSettingsDto> UpdateAsync(UpdateVideoSettingsDto input)
    {
        var settings = (await _repository.GetQueryableAsync()).FirstOrDefault();
        if (settings == null)
        {
            settings = new VideoSettings(
                GuidGenerator.Create(),
                input.EnableYoutubeTracking,
                input.ApiKey?.Trim(),
                input.FrequencyMinutes,
                CurrentTenant.Id);
            await _repository.InsertAsync(settings);
        }
        else
        {
            settings.EnableYoutubeTracking = input.EnableYoutubeTracking;
            settings.ApiKey = input.ApiKey?.Trim();
            settings.FrequencyMinutes = input.FrequencyMinutes;
            await _repository.UpdateAsync(settings);
        }

        return new VideoSettingsMapper().Map(settings);
    }
}
