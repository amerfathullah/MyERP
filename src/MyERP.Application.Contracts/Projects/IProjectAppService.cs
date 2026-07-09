using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Projects;

public interface IProjectAppService : IApplicationService
{
    Task<ProjectDto> GetAsync(Guid id);
    Task<PagedResultDto<ProjectDto>> GetListAsync(GetProjectListDto input);
    Task<ProjectDto> CreateAsync(CreateProjectDto input);
    Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectDto input);
    Task DeleteAsync(Guid id);
    Task<ProjectDto> CompleteAsync(Guid id);
    Task<ProjectDto> CancelAsync(Guid id);
    Task<ProjectTaskDto[]> GetTasksAsync(Guid projectId);
    Task<ProjectTaskDto> CreateTaskAsync(CreateProjectTaskDto input);
    Task<ProjectTaskDto> UpdateTaskAsync(Guid taskId, UpdateProjectTaskDto input);
    Task DeleteTaskAsync(Guid taskId);
    Task<ProjectTaskDto> StartTaskAsync(Guid taskId);
    Task<ProjectTaskDto> CompleteTaskAsync(Guid taskId);
    Task<ProjectTaskDto> CancelTaskAsync(Guid taskId);
}
