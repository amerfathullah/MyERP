using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Support;

public interface IIssuePriorityAppService : IApplicationService
{
    Task<IssuePriorityDto> GetAsync(Guid id);
    Task<PagedResultDto<IssuePriorityDto>> GetListAsync(GetIssuePriorityListDto input);
    Task<IssuePriorityDto> CreateAsync(CreateUpdateIssuePriorityDto input);
    Task<IssuePriorityDto> UpdateAsync(Guid id, CreateUpdateIssuePriorityDto input);
    Task DeleteAsync(Guid id);
}
