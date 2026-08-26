using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Utilities;

public interface IVideoAppService : ICrudAppService<VideoDto, Guid, GetVideoListDto, CreateUpdateVideoDto, CreateUpdateVideoDto>
{
    Task<VideoDto> UpdateStatsAsync(Guid id, UpdateVideoStatsDto input);
}
