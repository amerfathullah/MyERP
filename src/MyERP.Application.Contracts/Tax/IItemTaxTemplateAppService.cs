using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Tax;

public interface IItemTaxTemplateAppService : IApplicationService
{
    Task<PagedResultDto<ItemTaxTemplateDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<ItemTaxTemplateDto> GetAsync(Guid id);
    Task<ItemTaxTemplateDto> CreateAsync(CreateItemTaxTemplateDto input);
    Task<ItemTaxTemplateDto> UpdateAsync(Guid id, UpdateItemTaxTemplateDto input);
    Task DeleteAsync(Guid id);
}
