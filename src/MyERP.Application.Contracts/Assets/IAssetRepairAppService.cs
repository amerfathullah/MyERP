using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Assets;

public interface IAssetRepairAppService : IApplicationService
{
    Task<AssetRepairDto> GetAsync(Guid id);
    Task<PagedResultDto<AssetRepairDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<AssetRepairDto> CreateAsync(CreateUpdateAssetRepairDto input);
    Task<AssetRepairDto> UpdateAsync(Guid id, CreateUpdateAssetRepairDto input);
    Task DeleteAsync(Guid id);
    Task<AssetRepairDto> CompleteAsync(Guid id);
    Task<AssetRepairDto> CancelAsync(Guid id);
}
