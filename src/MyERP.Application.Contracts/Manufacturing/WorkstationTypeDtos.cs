using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public class WorkstationTypeDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal HourRate { get; set; }
    public WorkstationTypeCostDto[] Costs { get; set; } = [];
}

public class WorkstationTypeCostDto
{
    public string Component { get; set; } = null!;
    public decimal OperatingCost { get; set; }
}

public class CreateWorkstationTypeDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public CreateWorkstationTypeCostDto[] Costs { get; set; } = [];
}

public class CreateWorkstationTypeCostDto
{
    public string Component { get; set; } = null!;
    public decimal OperatingCost { get; set; }
}

public interface IWorkstationTypeAppService : IApplicationService
{
    Task<WorkstationTypeDto> GetAsync(Guid id);
    Task<PagedResultDto<WorkstationTypeDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<WorkstationTypeDto> CreateAsync(CreateWorkstationTypeDto input);
    Task<WorkstationTypeDto> UpdateAsync(Guid id, CreateWorkstationTypeDto input);
    Task DeleteAsync(Guid id);
}
