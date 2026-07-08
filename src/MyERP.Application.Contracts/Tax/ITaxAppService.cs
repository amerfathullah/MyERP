using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Tax;

public interface ITaxCategoryAppService : IApplicationService
{
    Task<TaxCategoryDto> GetAsync(Guid id);
    Task<PagedResultDto<TaxCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<TaxCategoryDto> CreateAsync(CreateUpdateTaxCategoryDto input);
    Task<TaxCategoryDto> UpdateAsync(Guid id, CreateUpdateTaxCategoryDto input);
    Task DeleteAsync(Guid id);
}

public interface ITaxRuleAppService : IApplicationService
{
    Task<PagedResultDto<TaxRuleDto>> GetListAsync(Guid taxCategoryId, PagedAndSortedResultRequestDto input);
    Task<TaxRuleDto> CreateAsync(CreateUpdateTaxRuleDto input);
    Task<TaxRuleDto> UpdateAsync(Guid id, CreateUpdateTaxRuleDto input);
    Task DeleteAsync(Guid id);
}
