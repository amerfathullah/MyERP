using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Purchasing;

// === Subcontracting Order DTOs ===

public class SubcontractingOrderDto : AuditedEntityDto<Guid>
{
    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public Guid CompanyId { get; set; }
    public decimal NetTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public SubcontractingOrderStatus Status { get; set; }
    public decimal PerReceived { get; set; }
    public Guid? SupplierWarehouseId { get; set; }
    public List<ScoItemDto> Items { get; set; } = new();
}

public class ScoItemDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal ReceivedQty { get; set; }
}

public class CreateSubcontractingOrderDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid SupplierId { get; set; }
    [Required] public DateTime OrderDate { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string? Notes { get; set; }
    public List<CreateScoItemDto> Items { get; set; } = new();
}

public class CreateScoItemDto
{
    [Required] public Guid ItemId { get; set; }
    [Required] public string ItemName { get; set; } = null!;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public Guid? BomId { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class GetScoListDto : PagedAndSortedResultRequestDto
{
    public SubcontractingOrderStatus? Status { get; set; }
    public Guid? CompanyId { get; set; }
}

// === Subcontracting Receipt DTOs ===

public class SubcontractingReceiptDto : AuditedEntityDto<Guid>
{
    public string ReceiptNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public Guid SupplierId { get; set; }
    public Guid SubcontractingOrderId { get; set; }
    public decimal NetTotal { get; set; }
    public SubcontractingReceiptStatus Status { get; set; }
    public List<SubcontractingReceiptItemDto> Items { get; set; } = new();
}

public class SubcontractingReceiptItemDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class CreateSubcontractingReceiptDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid SupplierId { get; set; }
    [Required] public Guid SubcontractingOrderId { get; set; }
    [Required] public DateTime PostingDate { get; set; }
    public Guid? WarehouseId { get; set; }
    public List<CreateScrItemDto> Items { get; set; } = new();
}

public class CreateScrItemDto
{
    [Required] public Guid ItemId { get; set; }
    [Required] public string ItemName { get; set; } = null!;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class RmTransferResultDto
{
    public Guid StockEntryId { get; set; }
    public string EntryNumber { get; set; } = null!;
    public int ItemCount { get; set; }
    public decimal TotalQty { get; set; }
}

// === Subcontracting Inward Order DTOs ===

public class SubcontractingInwardOrderDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public Guid SupplierId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? SubcontractingOrderId { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
    public decimal NetTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public SubcontractingInwardOrderStatus Status { get; set; }
    public decimal PerReceived { get; set; }
    public decimal PerBilled { get; set; }
    public List<SubcontractingInwardOrderItemDto> Items { get; set; } = new();
}

public class SubcontractingInwardOrderItemDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public Guid? BomId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal BilledQty { get; set; }
    public decimal PendingReceiptQty { get; set; }
    public Guid? WarehouseId { get; set; }
    public decimal ServiceCostPerQty { get; set; }
}

public class CreateSubcontractingInwardOrderDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid SupplierId { get; set; }
    [Required] public DateTime OrderDate { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? SubcontractingOrderId { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
    public List<CreateScioItemDto> Items { get; set; } = new();
}

public class CreateScioItemDto
{
    [Required] public Guid ItemId { get; set; }
    public Guid? BomId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public Guid? WarehouseId { get; set; }
    public decimal ServiceCostPerQty { get; set; }
}
