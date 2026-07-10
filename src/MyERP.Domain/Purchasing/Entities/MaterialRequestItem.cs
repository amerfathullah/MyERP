using System;
using Volo.Abp.Domain.Entities;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Line item in a Material Request.
/// </summary>
public class MaterialRequestItem : Entity<Guid>
{
    public Guid MaterialRequestId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public string Uom { get; set; } = "Unit";
    public Guid? WarehouseId { get; set; }

    protected MaterialRequestItem() { }

    public MaterialRequestItem(Guid id, Guid materialRequestId, Guid itemId,
        string itemName, decimal quantity, string uom, Guid? warehouseId = null)
        : base(id)
    {
        MaterialRequestId = materialRequestId;
        ItemId = itemId;
        ItemName = itemName;
        Quantity = quantity;
        Uom = uom;
        WarehouseId = warehouseId;
    }
}
