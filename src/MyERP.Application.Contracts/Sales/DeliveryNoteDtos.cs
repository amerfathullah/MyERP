using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class DeliveryNoteDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string DeliveryNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid WarehouseId { get; set; }
    public string? ShippingAddress { get; set; }
    public string? Transporter { get; set; }
    public string? TrackingNumber { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public bool IsReturn { get; set; }
    public Guid? ReturnAgainstId { get; set; }
    public string Status { get; set; } = null!;
    public List<DeliveryNoteItemDto> Items { get; set; } = new();
}

public class DeliveryNoteItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? SalesOrderItemId { get; set; }
}

public class CreateDeliveryNoteDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid CustomerId { get; set; }
    [Required] public Guid WarehouseId { get; set; }
    [Required] public DateTime PostingDate { get; set; }
    public Guid? SalesOrderId { get; set; }
    [StringLength(500)] public string? ShippingAddress { get; set; }
    [StringLength(200)] public string? Transporter { get; set; }
    [StringLength(100)] public string? TrackingNumber { get; set; }
    public bool IsReturn { get; set; }
    public Guid? ReturnAgainstId { get; set; }
    public string? Notes { get; set; }
    [Required][MinLength(1)] public List<CreateDeliveryNoteItemDto> Items { get; set; } = new();
}

public class CreateDeliveryNoteItemDto
{
    [Required] public Guid ItemId { get; set; }
    [Required][StringLength(500)] public string Description { get; set; } = null!;
    [Required][Range(0.0001, double.MaxValue)] public decimal Quantity { get; set; }
    [Required][Range(0, double.MaxValue)] public decimal UnitPrice { get; set; }
    [Range(0, double.MaxValue)] public decimal TaxAmount { get; set; }
    [StringLength(50)] public string Uom { get; set; } = "Unit";
    public Guid? SalesOrderItemId { get; set; }
}
