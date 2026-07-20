using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Line item in a purchase invoice.
/// Maps to ERPNext accounts/doctype/purchase_invoice_item.
/// </summary>
public class PurchaseInvoiceItem : CreationAuditedEntity<Guid>
{
    public Guid PurchaseInvoiceId { get; set; }
    public Guid ItemId { get; set; }

    public string Description { get; set; } = null!;
    public string Uom { get; set; } = "Unit";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;

    /// <summary>Item's stock UOM. From Item master.</summary>
    public string StockUom { get; set; } = "Unit";

    /// <summary>Conversion factor: transaction UOM → stock UOM.</summary>
    public decimal ConversionFactor { get; set; } = 1m;

    /// <summary>Quantity in stock UOM = Quantity × ConversionFactor.</summary>
    public decimal StockQty => Quantity * ConversionFactor;

    public Guid? TaxCategoryId { get; set; }

    /// <summary>Link to Purchase Order item (for billing qty tracking).</summary>
    public Guid? PurchaseOrderItemId { get; set; }

    /// <summary>Link to Purchase Receipt item (for receipt-to-bill traceability).</summary>
    public Guid? PurchaseReceiptItemId { get; set; }

    protected PurchaseInvoiceItem() { }

    public PurchaseInvoiceItem(
        Guid id, Guid purchaseInvoiceId, Guid itemId,
        string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom = "Unit")
        : base(id)
    {
        PurchaseInvoiceId = purchaseInvoiceId;
        ItemId = itemId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxAmount = taxAmount;
        Uom = uom;
    }
}
