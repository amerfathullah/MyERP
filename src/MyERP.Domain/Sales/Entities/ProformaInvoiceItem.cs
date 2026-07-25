using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Proforma Invoice Item — per-line detail linking back to Sales Order Item.
/// Per ERPNext PR #57263: proforma_invoice_item doctype.
/// </summary>
public class ProformaInvoiceItem : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProformaInvoiceId { get; set; }

    /// <summary>Links back to the Sales Order Item row.</summary>
    public Guid SalesOrderItemId { get; set; }

    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Uom { get; set; }

    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }

    protected ProformaInvoiceItem() { }

    public ProformaInvoiceItem(
        Guid id,
        Guid proformaInvoiceId,
        Guid salesOrderItemId,
        Guid itemId,
        string itemCode,
        string itemName,
        decimal quantity,
        decimal rate,
        string? uom = null)
        : base(id)
    {
        ProformaInvoiceId = proformaInvoiceId;
        SalesOrderItemId = salesOrderItemId;
        ItemId = itemId;
        ItemCode = itemCode;
        ItemName = itemName;
        Quantity = quantity;
        Rate = rate;
        Amount = Math.Round(quantity * rate, 4);
        Uom = uom;
    }
}
