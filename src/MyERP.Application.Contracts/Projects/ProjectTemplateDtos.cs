using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Projects;

public class ProjectTemplateTaskDto
{
    /// <summary>Caller-assigned key, unique within the request, used to wire dependency edges before ids exist.</summary>
    public Guid Key { get; set; }
    public string Subject { get; set; } = null!;
    public decimal TaskWeight { get; set; } = 1;
    public decimal ExpectedHours { get; set; }
    public bool IsMilestone { get; set; }
    public List<Guid> DependsOnKeys { get; set; } = new();
}

public class ProjectTemplateDto : EntityDto<Guid>
{
    public string TemplateName { get; set; } = null!;
    public bool Disabled { get; set; }
    public List<ProjectTemplateTaskDto> Tasks { get; set; } = new();
}

public class CreateUpdateProjectTemplateDto
{
    public string TemplateName { get; set; } = null!;
    public bool Disabled { get; set; }
    public List<ProjectTemplateTaskDto> Tasks { get; set; } = new();
}

public interface IProjectTemplateAppService : IApplicationService
{
    Task<ListResultDto<ProjectTemplateDto>> GetListAsync();
    Task<ProjectTemplateDto> GetAsync(Guid id);
    Task<ProjectTemplateDto> CreateAsync(CreateUpdateProjectTemplateDto input);
    Task<ProjectTemplateDto> UpdateAsync(Guid id, CreateUpdateProjectTemplateDto input);
    Task DeleteAsync(Guid id);
}
