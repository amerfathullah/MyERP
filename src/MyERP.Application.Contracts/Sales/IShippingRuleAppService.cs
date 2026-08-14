using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IShippingRuleAppService : IApplicationService
{
    Task<ShippingRuleDto> GetAsync(Guid id);
    Task<PagedResultDto<ShippingRuleDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<ShippingRuleDto> CreateAsync(CreateShippingRuleDto input);
    Task<ShippingRuleDto> ToggleAsync(Guid id, bool isEnabled);
    Task DeleteAsync(Guid id);
    Task<decimal> CalculateAsync(Guid ruleId, decimal value, string? countryCode = null);
}
