using System;
using System.Collections.Generic;
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
    public bool BatchSplit { get; set; }
    public decimal? WeightPerPiece { get; set; }
    public decimal ForQuantity { get; set; }
    public decimal CompletedQty { get; set; }
    public decimal PendingQty { get; set; }
    public decimal ProcessLossQty { get; set; }
    public decimal TotalTimeInMins { get; set; }
    public decimal PlannedTimeInMins { get; set; }
    public int SequenceId { get; set; }
    public int Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public JobCardTimeLogDto[] TimeLogs { get; set; } = [];
    public JobCardSecondaryItemDto[] SecondaryItems { get; set; } = [];
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
    public bool BatchSplit { get; set; }
    public decimal? WeightPerPiece { get; set; }
    public decimal ForQuantity { get; set; }
    public int SequenceId { get; set; }
    public decimal PlannedTimeInMins { get; set; }
    public List<CreateJobCardSecondaryItemDto> SecondaryItems { get; set; } = new();
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

public class JobCardRawMaterialDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Uom { get; set; } = "Unit";
    public Guid? SourceWarehouseId { get; set; }
    public string? SourceWarehouseName { get; set; }
    public Guid? WipWarehouseId { get; set; }
    public decimal RequiredQty { get; set; }
    public decimal TransferredQty { get; set; }
    public decimal PendingQty => Math.Max(0, RequiredQty - TransferredQty);
    public decimal StockQty { get; set; }
    public bool IsAvailable { get; set; }
}

public class JobCardSecondaryItemDto : EntityDto<Guid>
{
    public Guid JobCardId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal StockQty { get; set; }
    public string StockUom { get; set; } = string.Empty;
    public SecondaryItemType SecondaryItemType { get; set; }
    public Guid? BomSecondaryItemId { get; set; }
    public int Idx { get; set; }
}

public class CreateJobCardSecondaryItemDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal StockQty { get; set; }
    public string StockUom { get; set; } = string.Empty;
    public SecondaryItemType SecondaryItemType { get; set; }
    public Guid? BomSecondaryItemId { get; set; }
    public int? Idx { get; set; }
}

