using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Tax;

public interface ITaxWithholdingCategoryAppService : IApplicationService
{
    Task<PagedResultDto<TaxWithholdingCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<TaxWithholdingCategoryDto> GetAsync(Guid id);
    Task<TaxWithholdingCategoryDto> CreateAsync(CreateTaxWithholdingCategoryDto input);
    Task<TaxWithholdingCategoryDto> UpdateAsync(Guid id, UpdateTaxWithholdingCategoryDto input);
    Task DeleteAsync(Guid id);
}
