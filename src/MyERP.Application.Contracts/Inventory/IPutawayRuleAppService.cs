using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IPutawayRuleAppService : IApplicationService
{
    Task<PagedResultDto<PutawayRuleDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<PutawayRuleDto> GetAsync(Guid id);
    Task<PutawayRuleDto> CreateAsync(CreateUpdatePutawayRuleDto input);
    Task<PutawayRuleDto> UpdateAsync(Guid id, CreateUpdatePutawayRuleDto input);
    Task ToggleAsync(Guid id);
    Task DeleteAsync(Guid id);
}
