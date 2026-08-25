using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Projects;

public interface ITaskTypeAppService : IApplicationService
{
    Task<TaskTypeDto> GetAsync(Guid id);
    Task<PagedResultDto<TaskTypeDto>> GetListAsync(GetTaskTypeListDto input);
    Task<List<TaskTypeDto>> GetAllListAsync();
    Task<TaskTypeDto> CreateAsync(CreateUpdateTaskTypeDto input);
    Task<TaskTypeDto> UpdateAsync(Guid id, CreateUpdateTaskTypeDto input);
    Task DeleteAsync(Guid id);
}
