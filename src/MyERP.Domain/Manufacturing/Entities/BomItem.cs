using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Manufacturing.Entities;

public class BomItem : FullAuditedEntity<Guid>
{
    public Guid BomId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string? Uom { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public Guid? SourceWarehouseId { get; set; }

    protected BomItem() { }

    public BomItem(Guid id, Guid bomId, Guid itemId, string itemName, decimal quantity, decimal rate)
        : base(id)
    {
        BomId = bomId;
        ItemId = itemId;
        ItemName = itemName;
        Quantity = quantity;
        Rate = rate;
        Amount = quantity * rate;
    }

    public void Recalculate()
    {
        Amount = Quantity * Rate;
    }
}
