using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Tax;

public interface ITaxChargesTemplateAppService : IApplicationService
{
    Task<PagedResultDto<TaxChargesTemplateDto>> GetListAsync(GetTaxTemplateListDto input);
    Task<TaxChargesTemplateDto> GetAsync(Guid id);
    Task<TaxChargesTemplateDto?> GetDefaultAsync(Guid companyId, TaxTemplateType templateType, Guid? taxCategoryId = null);
    Task<List<TaxChargesTemplateDto>> GetActiveTemplatesAsync(Guid companyId, TaxTemplateType templateType);
    Task<TaxChargesTemplateDto> CreateAsync(CreateTaxChargesTemplateDto input);
    Task<TaxChargesTemplateDto> UpdateAsync(Guid id, CreateTaxChargesTemplateDto input);
    Task DeleteAsync(Guid id);
    Task<TaxChargesTemplateDto> ToggleEnabledAsync(Guid id);
}
