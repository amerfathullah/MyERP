using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public class IncotermDto : FullAuditedEntityDto<Guid>
{
    public string Code { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateIncotermDto
{
    [Required]
    [StringLength(10)]
    public string Code { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public interface IIncotermAppService : IApplicationService
{
    Task<IncotermDto> GetAsync(Guid id);
    Task<PagedResultDto<IncotermDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<IncotermDto> CreateAsync(CreateUpdateIncotermDto input);
    Task<IncotermDto> UpdateAsync(Guid id, CreateUpdateIncotermDto input);
    Task DeleteAsync(Guid id);
}
