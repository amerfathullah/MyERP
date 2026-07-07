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

    /// <summary>Tax category used for this line item.</summary>
    public Guid? TaxCategoryId { get; set; }

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
