using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Projects;

public class ProjectTypeDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
}

public class CreateUpdateProjectTypeDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

public interface IProjectTypeAppService : IApplicationService
{
    Task<ProjectTypeDto> GetAsync(Guid id);
    Task<PagedResultDto<ProjectTypeDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<ProjectTypeDto> CreateAsync(CreateUpdateProjectTypeDto input);
    Task<ProjectTypeDto> UpdateAsync(Guid id, CreateUpdateProjectTypeDto input);
    Task DeleteAsync(Guid id);
}
