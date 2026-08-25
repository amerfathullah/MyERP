using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Projects;

public interface IProjectUpdateAppService : IApplicationService
{
    Task<ProjectUpdateDto> GetAsync(Guid id);
    Task<PagedResultDto<ProjectUpdateDto>> GetListAsync(GetProjectUpdateListDto input);
    Task<ProjectUpdateDto> CreateAsync(CreateUpdateProjectUpdateDto input);
    Task<ProjectUpdateDto> UpdateAsync(Guid id, CreateUpdateProjectUpdateDto input);
    Task DeleteAsync(Guid id);
}
