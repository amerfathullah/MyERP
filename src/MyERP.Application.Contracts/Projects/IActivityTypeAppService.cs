using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Projects;

public interface IActivityTypeAppService : IApplicationService
{
    Task<List<ActivityTypeDto>> GetListAsync();
    Task<ActivityTypeDto> CreateAsync(CreateActivityTypeDto input);
    Task<ActivityTypeDto> UpdateAsync(Guid id, UpdateActivityTypeDto input);
    Task DeleteAsync(Guid id);
    Task<List<ActivityCostDto>> GetCostsForActivityAsync(Guid activityTypeId);
    Task<ActivityCostDto> SetEmployeeCostAsync(SetActivityCostDto input);
    Task DeleteCostAsync(Guid id);
}
