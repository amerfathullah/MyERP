using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using System.Threading.Tasks;

namespace MyERP.Manufacturing;

// === BOM DTOs ===

public class BomDto : AuditedEntityDto<Guid>
{
    public string BomNumber { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
    public string? Uom { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public decimal TotalMaterialCost { get; set; }
    public decimal OperatingCost { get; set; }
    public decimal TotalCost { get; set; }
    public decimal ProcessLossPercentage { get; set; }
    public decimal FgCostAllocationPercentage { get; set; }
    public Guid? ScrapWarehouseId { get; set; }
    public List<BomItemDto> Items { get; set; } = new();
    public List<BomOperationDto> Operations { get; set; } = new();
    public List<BomSecondaryItemDto> SecondaryItems { get; set; } = new();
}

public class BomOperationDto
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public Guid? WorkstationId { get; set; }
    public int SequenceId { get; set; }
    public decimal TimeInMins { get; set; }
    public decimal OperatingCost { get; set; }
    public int BatchSize { get; set; }
    public decimal FixedTime { get; set; }
    public string? Description { get; set; }
    public bool IsSubcontracted { get; set; }
}

public class BomItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string? Uom { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}

public class BomSecondaryItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public SecondaryItemType SecondaryItemType { get; set; }
    public decimal Quantity { get; set; }
    public decimal EffectiveQuantity { get; set; }
    public string? StockUom { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public decimal CostAllocationPercentage { get; set; }
    public decimal ProcessLossPercentage { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class CreateBomDto
{
    [Required] public Guid ItemId { get; set; }
    [Range(0.01, double.MaxValue)] public decimal Quantity { get; set; } = 1;
    public string? Uom { get; set; }
    [Required] public Guid CompanyId { get; set; }
    public bool IsDefault { get; set; }
    public Guid? SourceWarehouseId { get; set; }
    public Guid? TargetWarehouseId { get; set; }
    public Guid? RoutingId { get; set; }
    public Guid? ScrapWarehouseId { get; set; }
    public decimal ProcessLossPercentage { get; set; }
    public List<CreateBomItemDto> Items { get; set; } = new();
    public List<CreateBomOperationDto> Operations { get; set; } = new();
    public List<CreateBomSecondaryItemDto> SecondaryItems { get; set; } = new();
}

public class CreateBomSecondaryItemDto
{
    [Required] public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public SecondaryItemType SecondaryItemType { get; set; }
    [Range(0.01, double.MaxValue)] public decimal Quantity { get; set; }
    public string? StockUom { get; set; }
    [Range(0, double.MaxValue)] public decimal Rate { get; set; }
    [Range(0, 100)] public decimal CostAllocationPercentage { get; set; }
    [Range(0, 99.99)] public decimal ProcessLossPercentage { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class CreateBomOperationDto
{
    [Required] public Guid OperationId { get; set; }
    public Guid? WorkstationId { get; set; }
    [Range(1, int.MaxValue)] public int SequenceId { get; set; }
    [Range(0, double.MaxValue)] public decimal TimeInMins { get; set; }
    public int BatchSize { get; set; }
    public decimal FixedTime { get; set; }
    public string? Description { get; set; }
    public bool IsSubcontracted { get; set; }
    public decimal WorkstationHourRate { get; set; }
}

public class CreateBomItemDto
{
    [Required] public Guid ItemId { get; set; }
    [Required] public string ItemName { get; set; } = null!;
    [Range(0.01, double.MaxValue)] public decimal Quantity { get; set; }
    public string? Uom { get; set; }
    [Range(0, double.MaxValue)] public decimal Rate { get; set; }
}

// === Work Order DTOs ===

public class WorkOrderDto : AuditedEntityDto<Guid>
{
    public string WorkOrderNumber { get; set; } = null!;
    public WorkOrderStatus Status { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid BomId { get; set; }
    public decimal Quantity { get; set; }
    public decimal ProducedQuantity { get; set; }
    public decimal MaterialTransferred { get; set; }
    public decimal ProcessLossQty { get; set; }
    public decimal ProcessLossPercentage { get; set; }
    public decimal EffectiveFgQuantity { get; set; }
    public decimal PercentComplete { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public string? Notes { get; set; }
    public List<WorkOrderItemDto> RequiredItems { get; set; } = new();
}

public class WorkOrderItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal RequiredQuantity { get; set; }
    public decimal TransferredQuantity { get; set; }
    public decimal ConsumedQuantity { get; set; }
}

public class CreateWorkOrderDto
{
    [Required] public Guid ItemId { get; set; }
    [Required] public Guid BomId { get; set; }
    [Required] [Range(0.01, double.MaxValue)] public decimal Quantity { get; set; }
    [Required] public Guid CompanyId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? SourceWarehouseId { get; set; }
    public Guid? WipWarehouseId { get; set; }
    public Guid? FgWarehouseId { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    [StringLength(WorkOrderConsts.MaxNoteLength)] public string? Notes { get; set; }
}

public class GetWorkOrderListDto : PagedAndSortedResultRequestDto
{
    public WorkOrderStatus? Status { get; set; }
    public string? Filter { get; set; }
    public Guid? CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

// === Material Consumption DTO ===

public class ConsumptionItemDto
{
    [Required] public Guid ItemId { get; set; }
    [Required] [Range(0.0001, double.MaxValue)] public decimal Quantity { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? BatchId { get; set; }
}

public class CreateMaterialConsumptionDto
{
    [Required] public Guid WorkOrderId { get; set; }
    [Required] public List<ConsumptionItemDto> Items { get; set; } = new();
}

public class MaterialConsumptionResultDto
{
    public Guid StockEntryId { get; set; }
    public string EntryNumber { get; set; } = null!;
    public decimal TotalConsumedValue { get; set; }
    public int ItemCount { get; set; }
}

// === Interface ===

public interface IManufacturingAppService : IApplicationService
{
    // BOM
    Task<BomDto> GetBomAsync(Guid id);
    Task<PagedResultDto<BomDto>> GetBomListAsync(MyERP.Shared.CompanyFilteredPagedRequestDto input);
    Task<BomDto> CreateBomAsync(CreateBomDto input);
    Task<BomDto> UpdateBomAsync(Guid id, CreateBomDto input);
    Task DeleteBomAsync(Guid id);

    // Work Order
    Task<WorkOrderDto> GetWorkOrderAsync(Guid id);
    Task<PagedResultDto<WorkOrderDto>> GetWorkOrderListAsync(GetWorkOrderListDto input);
    Task<WorkOrderDto> CreateWorkOrderAsync(CreateWorkOrderDto input);
    Task DeleteWorkOrderAsync(Guid id);
    Task<WorkOrderDto> SubmitWorkOrderAsync(Guid id);
    Task<WorkOrderDto> StartWorkOrderAsync(Guid id);
    Task<WorkOrderDto> RecordProductionAsync(Guid id, decimal quantity, decimal processLossQty = 0);
    Task<WorkOrderDto> StopWorkOrderAsync(Guid id);
    Task<WorkOrderDto> UnstopWorkOrderAsync(Guid id);
    Task<WorkOrderDto> CancelWorkOrderAsync(Guid id);

    // Material Consumption
    Task<MaterialConsumptionResultDto> CreateMaterialConsumptionAsync(CreateMaterialConsumptionDto input);
}
