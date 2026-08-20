using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public class WorkstationDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? WorkstationType { get; set; }
    public Guid? WorkstationTypeId { get; set; }
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
    public Guid? WorkstationTypeId { get; set; }
    public int ProductionCapacity { get; set; } = 1;
    public string? Description { get; set; }
    public CreateWorkstationCostDto[] Costs { get; set; } = [];
}

public class CreateWorkstationCostDto
{
    public string Component { get; set; } = null!;
    public decimal OperatingCost { get; set; }
}

public interface IWorkstationAppService : IApplicationService
{
    Task<WorkstationDto> GetAsync(Guid id);
    Task<PagedResultDto<WorkstationDto>> GetListAsync(MyERP.Shared.CompanyFilteredPagedRequestDto input);
    Task<WorkstationDto> CreateAsync(CreateWorkstationDto input);
    Task<WorkstationDto> UpdateAsync(Guid id, CreateWorkstationDto input);
    Task<List<WorkstationUtilizationDto>> GetCapacityUtilizationAsync(Guid? companyId);
}

public class WorkstationUtilizationDto
{
    public Guid WorkstationId { get; set; }
    public string WorkstationName { get; set; } = null!;
    public string? WorkstationType { get; set; }
    public int ProductionCapacity { get; set; }
    public int ActiveJobCards { get; set; }
    public decimal UtilizationPercent { get; set; }
    public decimal TotalPlannedMinutes { get; set; }
    public decimal TotalActualMinutes { get; set; }
    public string Status { get; set; } = "Idle";
    public List<ActiveJobOnWorkstationDto> ActiveJobs { get; set; } = new();
}

public class ActiveJobOnWorkstationDto
{
    public Guid JobCardId { get; set; }
    public string? WorkOrderNumber { get; set; }
    public string? ItemName { get; set; }
    public string? OperationName { get; set; }
    public decimal ForQuantity { get; set; }
    public decimal CompletedQty { get; set; }
    public decimal PlannedTimeInMins { get; set; }
    public decimal ActualTimeInMins { get; set; }
    public int Status { get; set; }
}
