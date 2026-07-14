using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Line item in a purchase receipt.
/// Maps to ERPNext stock/doctype/purchase_receipt_item.
/// </summary>
public class PurchaseReceiptItem : CreationAuditedEntity<Guid>
{
    public Guid PurchaseReceiptId { get; set; }
    public Guid ItemId { get; set; }

    public string Description { get; set; } = null!;
    public string Uom { get; set; } = "Unit";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;

    /// <summary>Link back to the Purchase Order item being received.</summary>
    public Guid? PurchaseOrderItemId { get; set; }

    /// <summary>Source warehouse for internal transfer receipts. Must differ from target warehouse.</summary>
    public Guid? FromWarehouseId { get; set; }

    /// <summary>Target warehouse (item-level override). Falls back to receipt-level WarehouseId if null.</summary>
    public Guid? WarehouseId { get; set; }

    protected PurchaseReceiptItem() { }

    public PurchaseReceiptItem(
        Guid id, Guid purchaseReceiptId, Guid itemId,
        string description, decimal quantity, decimal unitPrice, decimal taxAmount,
        string uom = "Unit", Guid? purchaseOrderItemId = null)
        : base(id)
    {
        PurchaseReceiptId = purchaseReceiptId;
        ItemId = itemId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxAmount = taxAmount;
        Uom = uom;
        PurchaseOrderItemId = purchaseOrderItemId;
    }
}
