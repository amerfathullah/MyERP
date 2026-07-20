using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Sales.Entities;

/// <summary>
/// Line item in a sales invoice.
/// Maps to ERPNext accounts/doctype/sales_invoice_item.
/// </summary>
public class SalesInvoiceItem : CreationAuditedEntity<Guid>
{
    public Guid SalesInvoiceId { get; set; }
    public Guid ItemId { get; set; }

    public string Description { get; set; } = null!;
    public string Uom { get; set; } = "Unit";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }

    /// <summary>Quantity * UnitPrice (before tax).</summary>
    public decimal LineTotal => Quantity * UnitPrice;

    /// <summary>LineTotal + TaxAmount.</summary>
    public decimal LineTotalWithTax => LineTotal + TaxAmount;

    /// <summary>Item's stock UOM. From Item master.</summary>
    public string StockUom { get; set; } = "Unit";

    /// <summary>Conversion factor: transaction UOM → stock UOM.</summary>
    public decimal ConversionFactor { get; set; } = 1m;

    /// <summary>Quantity in stock UOM = Quantity × ConversionFactor.</summary>
    public decimal StockQty => Quantity * ConversionFactor;

    /// <summary>Tax category used for this line item.</summary>
    public Guid? TaxCategoryId { get; set; }

    /// <summary>Link to Sales Order item (for billing qty tracking).</summary>
    public Guid? SalesOrderItemId { get; set; }

    // Deferred Revenue fields
    /// <summary>Enable deferred revenue recognition for this line item.</summary>
    public bool EnableDeferredRevenue { get; set; }

    /// <summary>Account to post deferred revenue (liability until recognized).</summary>
    public Guid? DeferredRevenueAccountId { get; set; }

    /// <summary>Service period start date (for proration).</summary>
    public DateTime? ServiceStartDate { get; set; }

    /// <summary>Service period end date.</summary>
    public DateTime? ServiceEndDate { get; set; }

    // Valuation fields (for gross profit)
    /// <summary>Cost rate at time of billing (from stock valuation or last purchase rate).</summary>
    public decimal ValuationRate { get; set; }

    /// <summary>Gross profit per unit: UnitPrice - ValuationRate.</summary>
    public decimal GrossProfit => (UnitPrice - ValuationRate) * Quantity;

    protected SalesInvoiceItem() { }

    public SalesInvoiceItem(
        Guid id,
        Guid salesInvoiceId,
        Guid itemId,
        string description,
        decimal quantity,
        decimal unitPrice,
        decimal taxAmount,
        string uom = "Unit") : base(id)
    {
        SalesInvoiceId = salesInvoiceId;
        ItemId = itemId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxAmount = taxAmount;
        Uom = uom;
    }
}
