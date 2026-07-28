using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Purchasing;

public class PurchaseOrderDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string Status { get; set; } = null!;
    public decimal PerReceived { get; set; }
    public decimal PerBilled { get; set; }
    public string? Notes { get; set; }

    // Supplier Confirmation Tracking
    public string? SupplierConfirmationNumber { get; set; }
    public DateTime? SupplierConfirmationDate { get; set; }
    public DateTime? SupplierPromisedDate { get; set; }
    public bool IsSupplierConfirmed { get; set; }

    public List<PurchaseOrderItemDto> Items { get; set; } = new();
}

public class PurchaseOrderItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal BilledQty { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class CreatePurchaseOrderDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid SupplierId { get; set; }
    [Required] public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
    [Required][MinLength(1)] public List<CreatePurchaseOrderItemDto> Items { get; set; } = new();
}

public class CreatePurchaseOrderItemDto
{
    [Required] public Guid ItemId { get; set; }
    [Required][StringLength(500)] public string Description { get; set; } = null!;
    [Required][Range(0.0001, double.MaxValue)] public decimal Quantity { get; set; }
    [Required][Range(0, double.MaxValue)] public decimal UnitPrice { get; set; }
    [Range(0, double.MaxValue)] public decimal TaxAmount { get; set; }
    [StringLength(20)] public string Uom { get; set; } = "Unit";
    public Guid? WarehouseId { get; set; }
}

/// <summary>
/// Input for marking drop-ship PO items as delivered (no Purchase Receipt needed).
/// Per ERPNext PO.update_dropship_received_qty: updates received_qty directly on PO items
/// and cascades SO.delivered_qty for fulfillment tracking.
/// </summary>
public class UpdateDropShipDeliveredQtyDto
{
    [Required][MinLength(1)]
    public List<DropShipDeliveryItemDto> Items { get; set; } = new();
}

public class DropShipDeliveryItemDto
{
    /// <summary>PO Item ID to update.</summary>
    [Required] public Guid PurchaseOrderItemId { get; set; }

    /// <summary>
    /// Change in delivered qty (positive = more delivered, negative = correction).
    /// Per ERPNext: negative cannot exceed current received_qty; positive cannot exceed remaining qty.
    /// </summary>
    [Required] public decimal QtyChange { get; set; }
}

/// <summary>
/// Input for recording supplier acknowledgment/confirmation of a purchase order.
/// Per ERPNext: suppliers confirm receipt of PO and provide their reference number + promised delivery date.
/// </summary>
public class RecordSupplierConfirmationDto
{
    /// <summary>Supplier's own reference/confirmation number (e.g., their internal order number).</summary>
    [StringLength(100)]
    public string? ConfirmationNumber { get; set; }

    /// <summary>Date when supplier confirmed the order. Defaults to today if not provided.</summary>
    public DateTime? ConfirmationDate { get; set; }

    /// <summary>Supplier's promised delivery date (may differ from PO expected delivery date).</summary>
    public DateTime? PromisedDeliveryDate { get; set; }
}
