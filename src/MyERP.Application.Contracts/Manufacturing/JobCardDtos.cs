using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Manufacturing;

public class JobCardDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid OperationId { get; set; }
    public Guid? BomOperationId { get; set; }
    public Guid? WorkstationId { get; set; }
    public Guid? FinishedGoodItemId { get; set; }
    public Guid? SemiFgBomId { get; set; }
    public bool IsCorrective { get; set; }
    public decimal ForQuantity { get; set; }
    public decimal CompletedQty { get; set; }
    public decimal TotalTimeInMins { get; set; }
    public decimal PlannedTimeInMins { get; set; }
    public int SequenceId { get; set; }
    public int Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public JobCardTimeLogDto[] TimeLogs { get; set; } = [];
    public DateTime CreationTime { get; set; }
}

public class JobCardTimeLogDto
{
    public Guid Id { get; set; }
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
    public decimal TimeInMins { get; set; }
    public decimal CompletedQty { get; set; }
}

public class CreateJobCardDto
{
    public Guid CompanyId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid OperationId { get; set; }
    public Guid? WorkstationId { get; set; }
    public decimal ForQuantity { get; set; }
    public int SequenceId { get; set; }
    public decimal PlannedTimeInMins { get; set; }
}

public class AddTimeLogDto
{
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
    public decimal CompletedQty { get; set; }
}

public class GetJobCardListDto : PagedAndSortedResultRequestDto
{
    public Guid? WorkOrderId { get; set; }
    public Guid? CompanyId { get; set; }
    public JobCardStatus? Status { get; set; }
    public string? Filter { get; set; }
}
