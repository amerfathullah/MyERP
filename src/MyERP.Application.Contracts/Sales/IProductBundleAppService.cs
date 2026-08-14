using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IProductBundleAppService : IApplicationService
{
    Task<PagedResultDto<ProductBundleDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<ProductBundleDto> CreateAsync(CreateProductBundleDto input);
    Task<ProductBundleDto> DeactivateAsync(Guid id);
}
