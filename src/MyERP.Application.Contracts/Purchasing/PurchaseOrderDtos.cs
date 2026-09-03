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
    public Guid? PriceListId { get; set; }
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string Status { get; set; } = null!;
    public decimal PerReceived { get; set; }
    public decimal PerBilled { get; set; }
    public decimal AdvancePaid { get; set; }
    public decimal PerAdvancePaid { get; set; }
    public string AdvancePaymentStatus { get; set; } = "Not Initiated";
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
    public Guid? ExpenseAccountId { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    /// <summary>Whether this individual row is closed (per ERPNext PR #57596).</summary>
    public bool IsClosed { get; set; }
    public bool DeliveredBySupplier { get; set; }
}

public class CreatePurchaseOrderDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid SupplierId { get; set; }
    [Required] public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    /// <summary>Buying Price List. When omitted, defaults from Supplier.DefaultPriceListId.</summary>
    public Guid? PriceListId { get; set; }
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
    public Guid? ExpenseAccountId { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public bool DeliveredBySupplier { get; set; }
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

/// <summary>
/// Input for updating items on a submitted Purchase Order without cancel/amend.
/// Per ERPNext update_child_qty_rate: allows modifying qty, rate, and delivery dates on submitted orders
/// with guards against reducing below already-received/billed quantities.
/// </summary>
public class UpdateOrderItemsDto
{
    public List<UpdateOrderItemDto> Items { get; set; } = new();

    /// <summary>
    /// Item row IDs to remove from the order. Per ERPNext validate_child_on_delete (gotcha #6206):
    /// blocked if the row has already been received/delivered or billed.
    /// </summary>
    public List<Guid> RemovedItemIds { get; set; } = new();
}

public class UpdateOrderItemDto
{
    [Required] public Guid ItemId { get; set; }

    /// <summary>New quantity. Cannot be less than ReceivedQty for PO or DeliveredQty for SO.</summary>
    [Required][Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    /// <summary>New unit price. Cannot be less than billed amount per unit when BilledQty > 0.</summary>
    [Required][Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    /// <summary>Updated delivery date for this item (optional).</summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Updated warehouse (optional).</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Updated conversion factor to stock UOM (optional, per ERPNext PR #58603).</summary>
    public decimal? ConversionFactor { get; set; }
}

/// <summary>Result of update items operation with per-item validation details.</summary>
public class UpdateOrderItemsResultDto
{
    public int ItemsUpdated { get; set; }
    public decimal NewGrandTotal { get; set; }
    public decimal PreviousGrandTotal { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class PurchaseOrderTrackingBoardDto
{
    public List<TrackingBoardCardDto> Ordered { get; set; } = new();
    public List<TrackingBoardCardDto> PartiallyReceived { get; set; } = new();
    public List<TrackingBoardCardDto> FullyReceived { get; set; } = new();
    public List<TrackingBoardCardDto> Completed { get; set; } = new();
    public int TotalOrders { get; set; }
    public int OverdueCount { get; set; }
    public decimal TotalValue { get; set; }
}

public class TrackingBoardCardDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PerReceived { get; set; }
    public decimal PerBilled { get; set; }
    public string Stage { get; set; } = "Ordered";
    public bool IsOverdue { get; set; }
    public int DaysOverdue { get; set; }
    public int ItemCount { get; set; }
}

/// <summary>DTO for pending Material Request items available for PO creation.</summary>
public class PendingMaterialRequestItemDto
{
    public Guid MaterialRequestId { get; set; }
    public string MaterialRequestNumber { get; set; } = null!;
    public DateTime RequestDate { get; set; }
    public DateTime? RequiredByDate { get; set; }
    public Guid MaterialRequestItemId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal PendingQty { get; set; }
    public string Uom { get; set; } = "Unit";
    public Guid? WarehouseId { get; set; }
}

