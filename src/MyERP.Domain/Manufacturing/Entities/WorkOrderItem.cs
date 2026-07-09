using System;
using Volo.Abp.Domain.Entities;

namespace MyERP.Manufacturing.Entities;

public class WorkOrderItem : Entity<Guid>
{
    public Guid WorkOrderId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal RequiredQuantity { get; set; }
    public decimal TransferredQuantity { get; set; }
    public decimal ConsumedQuantity { get; set; }
    public Guid? SourceWarehouseId { get; set; }

    protected WorkOrderItem() { }

    public WorkOrderItem(Guid id, Guid workOrderId, Guid itemId, string itemName, decimal requiredQuantity)
        : base(id)
    {
        WorkOrderId = workOrderId;
        ItemId = itemId;
        ItemName = itemName;
        RequiredQuantity = requiredQuantity;
    }
}
