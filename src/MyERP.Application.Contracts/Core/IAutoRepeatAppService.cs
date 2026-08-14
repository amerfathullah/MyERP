using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IAutoRepeatAppService : IApplicationService
{
    Task<AutoRepeatDto> GetAsync(Guid id);
    Task<PagedResultDto<AutoRepeatDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<AutoRepeatDto> CreateAsync(CreateAutoRepeatDto input);
    Task EnableAsync(Guid id);
    Task DisableAsync(Guid id);
    Task DeleteAsync(Guid id);
}
