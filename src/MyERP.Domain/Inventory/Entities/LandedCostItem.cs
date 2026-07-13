using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Landed Cost Item — receipt item receiving allocated landed costs.
/// </summary>
public class LandedCostItem : FullAuditedEntity<Guid>
{
    public Guid LandedCostVoucherId { get; set; }
    public Guid ReceiptId { get; set; }

    /// <summary>Source document type: PurchaseReceipt, PurchaseInvoice, StockEntry, SubcontractingReceipt.</summary>
    public string ReceiptType { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string? Description { get; set; }

    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public decimal ApplicableCharges { get; set; }

    protected LandedCostItem() { }

    public LandedCostItem(Guid id, Guid landedCostVoucherId,
        Guid receiptId, string receiptType, Guid itemId,
        decimal quantity, decimal amount, string? description)
        : base(id)
    {
        LandedCostVoucherId = landedCostVoucherId;
        ReceiptId = receiptId;
        ReceiptType = receiptType;
        ItemId = itemId;
        Quantity = quantity;
        Amount = amount;
        Description = description;
    }
}
