using System;
using Volo.Abp.Domain.Entities;

namespace MyERP.Inventory.Entities;

/// <summary>
/// A preferred/approved supplier for an Item, with that supplier's part number.
/// An item can have multiple suppliers (used to suggest suppliers when creating
/// RFQs/Purchase Orders for this item). Maps to ERPNext's Item Supplier child table.
/// </summary>
public class ItemSupplier : Entity<Guid>
{
    public Guid ItemId { get; set; }
    public Guid SupplierId { get; set; }

    /// <summary>The supplier's own part number for this item (printed on POs for clarity).</summary>
    public string? SupplierPartNo { get; set; }

    protected ItemSupplier() { }

    public ItemSupplier(Guid id, Guid itemId, Guid supplierId, string? supplierPartNo = null)
        : base(id)
    {
        ItemId = itemId;
        SupplierId = supplierId;
        SupplierPartNo = supplierPartNo;
    }
}
