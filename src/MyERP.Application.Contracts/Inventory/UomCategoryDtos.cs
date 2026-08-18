using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public class UomCategoryDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
}

public class CreateUpdateUomCategoryDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = null!;
}

public interface IUomCategoryAppService : IApplicationService
{
    Task<UomCategoryDto> GetAsync(Guid id);
    Task<PagedResultDto<UomCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<UomCategoryDto> CreateAsync(CreateUpdateUomCategoryDto input);
    Task<UomCategoryDto> UpdateAsync(Guid id, CreateUpdateUomCategoryDto input);
    Task DeleteAsync(Guid id);
}
