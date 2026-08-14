using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Assets;

public interface IAssetValueAdjustmentAppService : IApplicationService
{
    Task<AssetValueAdjustmentDto> GetAsync(Guid id);
    Task<PagedResultDto<AssetValueAdjustmentDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<AssetValueAdjustmentDto> CreateAsync(CreateUpdateAssetValueAdjustmentDto input);
    Task<AssetValueAdjustmentDto> UpdateAsync(Guid id, CreateUpdateAssetValueAdjustmentDto input);
    Task DeleteAsync(Guid id);
    Task<AssetValueAdjustmentDto> SubmitAsync(Guid id);
    Task<AssetValueAdjustmentDto> CancelAsync(Guid id);
}
