using System;
using Volo.Abp.Domain.Entities;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Planned finished good item in a Production Plan (from SO/MR demand).
/// </summary>
public class ProductionPlanItem : Entity<Guid>
{
    public Guid ProductionPlanId { get; set; }

    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public Guid BomId { get; set; }

    public decimal PlannedQty { get; set; }
    public decimal ProducedQty { get; set; }

    public Guid? WarehouseId { get; set; }
    public DateTime? PlannedStartDate { get; set; }

    /// <summary>Source Sales Order, if demand originated from SO.</summary>
    public Guid? SalesOrderId { get; set; }

    /// <summary>Source Material Request, if demand originated from MR.</summary>
    public Guid? MaterialRequestId { get; set; }

    /// <summary>Generated Work Order ID (populated after WO generation).</summary>
    public Guid? WorkOrderId { get; set; }

    protected ProductionPlanItem() { }

    public ProductionPlanItem(Guid id, Guid productionPlanId, Guid itemId, string itemName, Guid bomId, decimal plannedQty)
        : base(id)
    {
        ProductionPlanId = productionPlanId;
        ItemId = itemId;
        ItemName = itemName;
        BomId = bomId;
        PlannedQty = plannedQty;
    }
}
