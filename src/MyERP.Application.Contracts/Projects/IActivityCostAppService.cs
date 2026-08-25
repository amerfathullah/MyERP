using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Projects;

public interface IActivityCostAppService : IApplicationService
{
    Task<ActivityCostDto> GetAsync(Guid id);
    Task<PagedResultDto<ActivityCostDto>> GetListAsync(GetActivityCostListDto input);
    Task<ActivityCostDto> CreateAsync(CreateUpdateActivityCostDto input);
    Task<ActivityCostDto> UpdateAsync(Guid id, CreateUpdateActivityCostDto input);
    Task DeleteAsync(Guid id);
}
