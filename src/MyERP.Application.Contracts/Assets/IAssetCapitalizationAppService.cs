using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Assets;

public interface IAssetCapitalizationAppService : IApplicationService
{
    Task<AssetCapitalizationDto> GetAsync(Guid id);
    Task<PagedResultDto<AssetCapitalizationDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<AssetCapitalizationDto> CreateAsync(CreateUpdateAssetCapitalizationDto input);
    Task<AssetCapitalizationDto> UpdateAsync(Guid id, CreateUpdateAssetCapitalizationDto input);
    Task DeleteAsync(Guid id);
    Task<AssetCapitalizationDto> SubmitAsync(Guid id);
    Task<AssetCapitalizationDto> CancelAsync(Guid id);
}
