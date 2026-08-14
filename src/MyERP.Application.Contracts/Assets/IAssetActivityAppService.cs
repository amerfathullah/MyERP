using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Assets;

public interface IAssetActivityAppService : IApplicationService
{
    Task<List<AssetActivityDto>> GetListByAssetAsync(Guid assetId);
    Task<AssetActivityDto> CreateAsync(CreateAssetActivityDto input);
}
