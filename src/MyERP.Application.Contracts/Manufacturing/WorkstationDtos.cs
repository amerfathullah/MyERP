using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public class WorkstationDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? WorkstationType { get; set; }
    public int ProductionCapacity { get; set; }
    public decimal HourRate { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public WorkstationCostDto[] Costs { get; set; } = [];
    public WorkstationWorkingHourDto[] WorkingHours { get; set; } = [];
}

public class WorkstationCostDto
{
    public string Name { get; set; } = null!;
    public decimal Amount { get; set; }
}

public class WorkstationWorkingHourDto
{
    public string DayOfWeek { get; set; } = null!;
    public string StartTime { get; set; } = null!;
    public string EndTime { get; set; } = null!;
}

public class CreateWorkstationDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string? WorkstationType { get; set; }
    public int ProductionCapacity { get; set; } = 1;
    public string? Description { get; set; }
}

public interface IWorkstationAppService : IApplicationService
{
    Task<WorkstationDto> GetAsync(Guid id);
    Task<PagedResultDto<WorkstationDto>> GetListAsync(MyERP.Shared.CompanyFilteredPagedRequestDto input);
    Task<WorkstationDto> CreateAsync(CreateWorkstationDto input);
}
