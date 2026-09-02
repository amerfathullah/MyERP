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
    public bool SetQtyBasedOnPercentage { get; set; }
    public List<BomItemDto> Items { get; set; } = new();
    public List<BomOperationDto> Operations { get; set; } = new();
    public List<BomSecondaryItemDto> SecondaryItems { get; set; } = new();
}

public class ReplaceBomDto
{
    [Required]
    public Guid CurrentBomId { get; set; }

    [Required]
    public Guid NewBomId { get; set; }
}

public class ReplaceBomResultDto
{
    public int UpdatedBomItemCount { get; set; }
    public int RecostedBomCount { get; set; }
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
    public bool QualityInspectionRequired { get; set; }
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
    public decimal Percentage { get; set; }
    public bool IsBalanceItem { get; set; }
}

public class BomSecondaryItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public SecondaryItemType SecondaryItemType { get; set; }
    public SecondaryItemValuationType ValuationType { get; set; }
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
    public bool SetQtyBasedOnPercentage { get; set; }
    public List<CreateBomItemDto> Items { get; set; } = new();
    public List<CreateBomOperationDto> Operations { get; set; } = new();
    public List<CreateBomSecondaryItemDto> SecondaryItems { get; set; } = new();
}

public class CreateBomSecondaryItemDto
{
    [Required] public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public SecondaryItemType SecondaryItemType { get; set; }
    public SecondaryItemValuationType ValuationType { get; set; } = SecondaryItemValuationType.ValuationRate;
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
    public bool QualityInspectionRequired { get; set; }
    public decimal WorkstationHourRate { get; set; }
}

public class CreateBomItemDto
{
    [Required] public Guid ItemId { get; set; }
    [Required] public string ItemName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string? Uom { get; set; }
    [Range(0, double.MaxValue)] public decimal Rate { get; set; }
    public decimal Percentage { get; set; }
    public bool IsBalanceItem { get; set; }
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
    public decimal DisassembledQuantity { get; set; }
    public decimal MaterialTransferred { get; set; }
    public decimal ProcessLossQty { get; set; }
    public decimal ProcessLossPercentage { get; set; }
    public decimal EffectiveFgQuantity { get; set; }
    public decimal PercentComplete { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? SalesOrderItemId { get; set; }
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
    public Guid? SalesOrderItemId { get; set; }
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

/// <summary>Input for creating a Disassembly Stock Entry from Work Order.</summary>
public class CreateDisassemblyDto
{
    [Required] public Guid WorkOrderId { get; set; }
    [Required] [Range(0.0001, double.MaxValue)] public decimal Quantity { get; set; }
    public Guid? SourceStockEntryId { get; set; }
}

/// <summary>Result DTO for Disassembly Stock Entry creation.</summary>
public class DisassemblyResultDto
{
    public Guid StockEntryId { get; set; }
    public string EntryNumber { get; set; } = null!;
    public decimal DisassembledQty { get; set; }
    public int ItemCount { get; set; }
    public decimal RemainingDisassemblable { get; set; }
}

/// <summary>Production cost breakdown per ERPNext BOM Costing + WO actual cost tracking.</summary>
public class ProductionCostBreakdownDto
{
    public Guid WorkOrderId { get; set; }
    public string? WorkOrderNumber { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal ProducedQty { get; set; }
    public decimal TotalRmCost { get; set; }
    public decimal ProcessLossQty { get; set; }
    public decimal ProcessLossValue { get; set; }
    public decimal AdditionalCosts { get; set; }
    public decimal TotalProductionCost { get; set; }
    public decimal FgUnitCost { get; set; }
    public decimal BomStandardCost { get; set; }
    public decimal CostVariance { get; set; }
    public decimal CostVariancePercent { get; set; }
}

/// <summary>Result DTO for Stock Entry creation from Work Order.</summary>
public class StockEntryResultDto
{
    public Guid StockEntryId { get; set; }
    public string? EntryNumber { get; set; }
    public string? EntryType { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalValue { get; set; }
}

/// <summary>Input for creating a Manufacture Stock Entry from Work Order.</summary>
public class CreateManufactureStockEntryDto
{
    [Required] public Guid WorkOrderId { get; set; }
    [Required] [Range(0.0001, double.MaxValue)] public decimal FgQuantity { get; set; }
    public decimal ProcessLossQty { get; set; }
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
    Task<BomDto> UpdateBomCostAsync(Guid bomId);
    Task<ReplaceBomResultDto> ReplaceBomAsync(ReplaceBomDto input);
    Task<SubcontractingBomItemsDto> GetBomItemsForSubcontractingAsync(Guid itemId, Guid companyId, decimal fgQty = 1);

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
    Task<WorkOrderDto> CloseWorkOrderAsync(Guid id);
    Task<WorkOrderDto> CancelWorkOrderAsync(Guid id);

    // Material Consumption
    Task<MaterialConsumptionResultDto> CreateMaterialConsumptionAsync(CreateMaterialConsumptionDto input);

    // Material Transfer & Manufacture Stock Entry from Work Order
    Task<StockEntryResultDto> CreateMaterialTransferForManufactureAsync(Guid workOrderId);
    Task<StockEntryResultDto> CreateManufactureStockEntryAsync(CreateManufactureStockEntryDto input);

    // Job Cards for Work Order
    Task<PagedResultDto<WorkOrderJobCardDto>> GetWorkOrderJobCardsAsync(Guid workOrderId);
    Task<List<WorkOrderJobCardDto>> CreateJobCardsForWorkOrderAsync(Guid workOrderId);

    // Disassembly
    Task<DisassemblyResultDto> CreateDisassemblyStockEntryAsync(CreateDisassemblyDto input);

    // Batch WO from Sales Order
    Task<BatchCreateWorkOrdersResultDto> CreateWorkOrdersFromSalesOrderAsync(Guid salesOrderId);

    // Cost Analysis
    Task<ProductionCostBreakdownDto> GetProductionCostBreakdownAsync(Guid workOrderId);

    // Planning & Reporting
    Task<ProductionScheduleDto> GetProductionScheduleAsync(Guid companyId);
    Task<MaterialShortageAcrossOrdersDto> GetMaterialShortageAcrossOrdersAsync(Guid companyId);

    // Material Availability & Readiness
    Task<List<MaterialAvailabilityDto>> GetMaterialAvailabilityAsync(Guid workOrderId);
    Task<List<WorkOrderMaterialReadinessDto>> GetBatchMaterialReadinessAsync(Guid? companyId);
}

/// <summary>
/// Lightweight Job Card summary for Work Order operations progress display.
/// </summary>
public class WorkOrderJobCardDto
{
    public Guid Id { get; set; }
    public int SequenceId { get; set; }
    public Guid OperationId { get; set; }
    public int Status { get; set; }
    public decimal ForQuantity { get; set; }
    public decimal CompletedQty { get; set; }
    public decimal TotalTimeInMins { get; set; }
    public decimal PlannedTimeInMins { get; set; }
    public string? OperationName { get; set; }
}

// === Subcontracting BOM DTOs ===

/// <summary>
/// Returned by GetBomItemsForSubcontractingAsync — BOM raw materials for subcontracting PO creation.
/// Per ERPNext: when creating a subcontracting PO, BOM components auto-populate as supplied items.
/// </summary>
public class SubcontractingBomItemsDto
{
    public Guid? BomId { get; set; }
    public string? BomNumber { get; set; }
    public Guid? FgItemId { get; set; }
    public decimal FgQty { get; set; }
    public Guid? SourceWarehouseId { get; set; }
    public List<SubcontractingBomItemLineDto> Items { get; set; } = new();
}

public class SubcontractingBomItemLineDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public string ItemCode { get; set; } = "";
    public decimal RequiredQty { get; set; }
    public decimal Rate { get; set; }
    public string Uom { get; set; } = "Unit";
    public Guid? SourceWarehouseId { get; set; }
}

/// <summary>
/// Per-item material availability for a Work Order.
/// Per ERPNext: shown before starting production to verify material readiness.
/// </summary>
public class MaterialAvailabilityDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = "—";
    public string ItemCode { get; set; } = "—";
    public decimal RequiredQty { get; set; }
    public decimal TransferredQty { get; set; }
    public decimal PendingQty { get; set; }
    public decimal AvailableQty { get; set; }
    public decimal Shortage { get; set; }
    public bool HasSufficientStock { get; set; }
    public Guid WarehouseId { get; set; }
}

/// <summary>Per-WO material readiness summary for dashboard display.</summary>
public class WorkOrderMaterialReadinessDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = "—";
    public string ItemName { get; set; } = "—";

    /// <summary>All materials available — production can start immediately.</summary>
    public bool IsReady { get; set; }

    /// <summary>Some materials transferred but not all.</summary>
    public bool IsPartial { get; set; }

    /// <summary>Critical shortage exists — production blocked.</summary>
    public bool HasShortage { get; set; }

    public int TotalMaterials { get; set; }
    public int MaterialsAvailable { get; set; }
    public int MaterialsShort { get; set; }
    public decimal TotalShortageValue { get; set; }

    /// <summary>"Ready" | "Partial" | "Blocked"</summary>
    public string ReadinessStatus => IsReady ? "Ready" : HasShortage ? "Blocked" : "Partial";
}

// === Production Schedule DTOs ===

public class ProductionScheduleItemDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal ProducedQuantity { get; set; }
    public decimal PercentComplete { get; set; }
    public int Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public bool IsOverdue { get; set; }
    public int DaysOverdue { get; set; }
    public string StatusColor { get; set; } = "secondary";
}

public class ProductionScheduleDto
{
    public List<ProductionScheduleItemDto> Items { get; set; } = new();
    public int TotalOrders { get; set; }
    public int NotStarted { get; set; }
    public int InProcess { get; set; }
    public int Completed { get; set; }
    public int Overdue { get; set; }
    public decimal OverallCompletionRate { get; set; }
}

// === Cross-WO Material Shortage Summary DTOs ===

public class MaterialShortageItemDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal TotalRequired { get; set; }
    public decimal TotalAvailable { get; set; }
    public decimal ShortageQty { get; set; }
    public int AffectedWorkOrders { get; set; }
    public string MostUrgentWO { get; set; } = string.Empty;
}

public class MaterialShortageAcrossOrdersDto
{
    public List<MaterialShortageItemDto> Items { get; set; } = new();
    public int TotalItemsShort { get; set; }
    public int TotalAffectedOrders { get; set; }
    public decimal TotalShortageValue { get; set; }
}

// === Batch Work Order Creation DTOs ===

public class BatchCreateWorkOrdersResultDto
{
    public int CreatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<CreatedWorkOrderInfo> WorkOrders { get; set; } = new();
}

public class CreatedWorkOrderInfo
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
}
