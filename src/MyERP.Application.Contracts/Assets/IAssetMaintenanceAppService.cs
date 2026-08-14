using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Assets;

public interface IAssetMaintenanceAppService :
    ICrudAppService<
        AssetMaintenanceDto,
        Guid,
        GetAssetMaintenanceListDto,
        CreateUpdateAssetMaintenanceDto>
{
    Task<AssetMaintenanceDto> GetByAssetAsync(Guid assetId);
}
