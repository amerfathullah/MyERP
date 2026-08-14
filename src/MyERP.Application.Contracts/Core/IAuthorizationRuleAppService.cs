using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IAuthorizationRuleAppService : IApplicationService
{
    Task<AuthorizationRuleDto> GetAsync(Guid id);
    Task<PagedResultDto<AuthorizationRuleDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<AuthorizationRuleDto> CreateAsync(CreateAuthorizationRuleDto input);
    Task<AuthorizationRuleDto> UpdateAsync(Guid id, UpdateAuthorizationRuleDto input);
    Task DeleteAsync(Guid id);
}
