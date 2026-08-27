using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class BatchDto : AuditedEntityDto<Guid>
{
    public string BatchNo { get; set; } = null!;
    public Guid ItemId { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? ShelfLifeInDays { get; set; }
    public string? SupplierBatchNo { get; set; }
    public bool IsDisabled { get; set; }
    public bool IsExpired { get; set; }
    public string? Description { get; set; }
}

public class CreateBatchDto
{
    [Required] public Guid ItemId { get; set; }
    [Required][StringLength(100)] public string BatchNo { get; set; } = null!;
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? ShelfLifeInDays { get; set; }
    [StringLength(100)] public string? SupplierBatchNo { get; set; }
    [StringLength(500)] public string? Description { get; set; }
}

public class GetBatchListDto : PagedAndSortedResultRequestDto
{
    public Guid? ItemId { get; set; }
    public bool? IsDisabled { get; set; }
    public string? Filter { get; set; }
}

public class BatchStockBalanceDto
{
    public Guid BatchId { get; set; }
    public string BatchNo { get; set; } = null!;
    public Guid ItemId { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public List<BatchWarehouseBalanceDto> WarehouseBalances { get; set; } = new();
}

public class BatchWarehouseBalanceDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal StockValue { get; set; }
    public decimal ValuationRate { get; set; }
}

public class BatchMovementHistoryDto
{
    public Guid BatchId { get; set; }
    public string BatchNo { get; set; } = null!;
    public List<BatchMovementEntryDto> Entries { get; set; } = new();
}

public class BatchMovementEntryDto
{
    public Guid Id { get; set; }
    public DateTime PostingDate { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public decimal QuantityChange { get; set; }
    public decimal ValuationRate { get; set; }
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }
    public bool IsInward { get; set; }
}

public class BatchTraceabilityDto
{
    public Guid BatchId { get; set; }
    public string BatchNo { get; set; } = null!;
    public Guid ItemId { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal TotalProduced { get; set; }
    public decimal TotalDelivered { get; set; }
    public int CustomerCount { get; set; }
    public List<BatchDeliveryTraceDto> Deliveries { get; set; } = new();
    public List<BatchCustomerSummaryDto> CustomerSummary { get; set; } = new();
}

public class BatchDeliveryTraceDto
{
    public Guid DeliveryNoteId { get; set; }
    public string? DeliveryNumber { get; set; }
    public DateTime DeliveryDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public decimal QuantityDelivered { get; set; }
    public Guid WarehouseId { get; set; }
}

public class BatchCustomerSummaryDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public decimal TotalQuantity { get; set; }
    public int DeliveryCount { get; set; }
    public DateTime FirstDeliveryDate { get; set; }
    public DateTime LastDeliveryDate { get; set; }
}

public class SplitBatchDto
{
    [Required] public Guid SourceBatchId { get; set; }
    [Required][StringLength(100)] public string NewBatchNo { get; set; } = null!;
    [Required] public Guid WarehouseId { get; set; }
    [Range(0.0001, double.MaxValue)] public decimal SplitQuantity { get; set; }
    [StringLength(500)] public string? Description { get; set; }
}

public class SplitBatchResultDto
{
    public Guid NewBatchId { get; set; }
    public string NewBatchNo { get; set; } = null!;
    public Guid StockEntryId { get; set; }
    public string? StockEntryNumber { get; set; }
}

public class MoveBatchDto
{
    [Required] public Guid BatchId { get; set; }
    [Required] public Guid SourceWarehouseId { get; set; }
    [Required] public Guid TargetWarehouseId { get; set; }
    [Range(0.0001, double.MaxValue)] public decimal Quantity { get; set; }
    [StringLength(500)] public string? Description { get; set; }
}

public class MoveBatchResultDto
{
    public Guid BatchId { get; set; }
    public Guid StockEntryId { get; set; }
    public string? StockEntryNumber { get; set; }
}
