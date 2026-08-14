using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Assets;

public interface IAssetCategoryAppService : IApplicationService
{
    Task<AssetCategoryDto> GetAsync(Guid id);
    Task<PagedResultDto<AssetCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<AssetCategoryDto> CreateAsync(CreateUpdateAssetCategoryDto input);
    Task<AssetCategoryDto> UpdateAsync(Guid id, CreateUpdateAssetCategoryDto input);
    Task DeleteAsync(Guid id);
    Task<AssetCategoryAccountDto?> GetAccountForCompanyAsync(Guid categoryId, Guid companyId);
}
