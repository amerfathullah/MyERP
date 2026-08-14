using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IPricingRuleAppService : IApplicationService
{
    Task<PagedResultDto<PricingRuleDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<PricingRuleDto> GetAsync(Guid id);
    Task<PricingRuleDto> CreateAsync(CreatePricingRuleDto input);
    Task<List<PricingRuleResultDto>> ApplyAsync(ApplyPricingRuleDto input);
    Task DeleteAsync(Guid id);
}
