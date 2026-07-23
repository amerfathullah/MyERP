using System;
using Volo.Abp.Domain.Entities;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Work Order Item — raw material required for production.
/// Per ERPNext work_order.instructions.md: required items auto-populated from BOM.
/// Per gotcha #505: WO secondary_items is COMPUTED (not stored child table).
/// </summary>
public class WorkOrderItem : Entity<Guid>
{
    public Guid WorkOrderId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal RequiredQuantity { get; set; }
    public decimal TransferredQuantity { get; set; }
    public decimal ConsumedQuantity { get; set; }
    public Guid? SourceWarehouseId { get; set; }

    /// <summary>
    /// Per ERPNext: operation_row_id links RM to specific BOM operation for
    /// semi-finished-goods tracking. When track_semi_finished_goods is enabled,
    /// each operation has its own set of raw materials.
    /// </summary>
    public Guid? BomOperationId { get; set; }

    /// <summary>
    /// Stock UOM for the item (denormalized for SLE creation).
    /// </summary>
    public string StockUom { get; set; } = "Unit";

    /// <summary>
    /// Conversion factor from transaction UOM to stock UOM.
    /// Per ERPNext: stock_qty = qty × conversion_factor.
    /// </summary>
    public decimal ConversionFactor { get; set; } = 1m;

    /// <summary>Stock quantity = RequiredQuantity × ConversionFactor.</summary>
    public decimal StockQty => RequiredQuantity * ConversionFactor;

    /// <summary>Pending transfer quantity (required - already transferred).</summary>
    public decimal PendingTransferQty => Math.Max(0, RequiredQuantity - TransferredQuantity);

    /// <summary>Pending consumption (transferred - already consumed). Only relevant for MaterialTransferred backflush.</summary>
    public decimal AvailableForConsumption => Math.Max(0, TransferredQuantity - ConsumedQuantity);

    /// <summary>Whether this item is an alternative item substitution.</summary>
    public bool IsAlternativeItem { get; set; }

    /// <summary>Original item code if this is an alternative substitution.</summary>
    public Guid? OriginalItemId { get; set; }

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
