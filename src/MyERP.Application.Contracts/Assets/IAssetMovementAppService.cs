using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Assets;

public interface IAssetMovementAppService : IApplicationService
{
    Task<AssetMovementDto> GetAsync(Guid id);
    Task<PagedResultDto<AssetMovementDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<AssetMovementDto> CreateAsync(CreateUpdateAssetMovementDto input);
    Task<AssetMovementDto> UpdateAsync(Guid id, CreateUpdateAssetMovementDto input);
    Task DeleteAsync(Guid id);
    Task<AssetMovementDto> SubmitAsync(Guid id);
    Task<AssetMovementDto> CancelAsync(Guid id);
}
