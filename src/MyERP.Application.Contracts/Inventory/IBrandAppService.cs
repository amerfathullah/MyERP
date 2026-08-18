using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IBrandAppService : IApplicationService
{
    Task<PagedResultDto<BrandDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<BrandDto> GetAsync(Guid id);
    Task<BrandDto> CreateAsync(CreateUpdateBrandDto input);
    Task<BrandDto> UpdateAsync(Guid id, CreateUpdateBrandDto input);
    Task DeleteAsync(Guid id);
}
