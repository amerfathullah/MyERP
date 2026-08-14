using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Assets;

public interface IAssetMaintenanceLogAppService :
    ICrudAppService<
        AssetMaintenanceLogDto,
        Guid,
        GetAssetMaintenanceLogListDto,
        CreateUpdateAssetMaintenanceLogDto>
{
    Task<AssetMaintenanceLogDto> CompleteAsync(Guid id, CompleteAssetMaintenanceLogDto input);
    Task<AssetMaintenanceLogDto> CancelAsync(Guid id);
    Task<List<AssetMaintenanceLogDto>> GetLogsByAssetAsync(Guid assetId);
}
