using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Sales.Entities;

/// <summary>
/// Line item in a delivery note.
/// Maps to ERPNext stock/doctype/delivery_note_item.
/// </summary>
public class DeliveryNoteItem : CreationAuditedEntity<Guid>
{
    public Guid DeliveryNoteId { get; set; }
    public Guid ItemId { get; set; }

    public string Description { get; set; } = null!;
    public string Uom { get; set; } = "Unit";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;

    /// <summary>Link back to the Sales Order item being fulfilled.</summary>
    public Guid? SalesOrderItemId { get; set; }

    protected DeliveryNoteItem() { }

    public DeliveryNoteItem(
        Guid id, Guid deliveryNoteId, Guid itemId,
        string description, decimal quantity, decimal unitPrice, decimal taxAmount,
        string uom = "Unit", Guid? salesOrderItemId = null)
        : base(id)
    {
        DeliveryNoteId = deliveryNoteId;
        ItemId = itemId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxAmount = taxAmount;
        Uom = uom;
        SalesOrderItemId = salesOrderItemId;
    }
}
