using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Purchasing;

public class PurchaseReceiptDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string ReceiptNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public Guid WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string? SupplierDeliveryNote { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public bool IsReturn { get; set; }
    public Guid? ReturnAgainstId { get; set; }
    public string Status { get; set; } = null!;
    public List<PurchaseReceiptItemDto> Items { get; set; } = new();
}

public class PurchaseReceiptItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? PurchaseOrderItemId { get; set; }
}

public class CreatePurchaseReceiptDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid SupplierId { get; set; }
    [Required] public Guid WarehouseId { get; set; }
    [Required] public DateTime PostingDate { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    [StringLength(100)] public string? SupplierDeliveryNote { get; set; }
    public bool IsReturn { get; set; }
    public Guid? ReturnAgainstId { get; set; }
    public string? Notes { get; set; }
    [Required][MinLength(1)] public List<CreatePurchaseReceiptItemDto> Items { get; set; } = new();
}

public class CreatePurchaseReceiptItemDto
{
    [Required] public Guid ItemId { get; set; }
    [Required][StringLength(500)] public string Description { get; set; } = null!;
    [Required][Range(0.0001, double.MaxValue)] public decimal Quantity { get; set; }
    [Required][Range(0, double.MaxValue)] public decimal UnitPrice { get; set; }
    [Range(0, double.MaxValue)] public decimal TaxAmount { get; set; }
    [StringLength(50)] public string Uom { get; set; } = "Unit";
    public Guid? PurchaseOrderItemId { get; set; }
}
