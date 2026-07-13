using System;
using Volo.Abp.Domain.Entities;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Material requirement line in a Production Plan (from BOM explosion).
/// </summary>
public class ProductionPlanMrItem : Entity<Guid>
{
    public Guid ProductionPlanId { get; set; }

    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;

    /// <summary>Total quantity required from BOM explosion.</summary>
    public decimal RequiredQty { get; set; }

    /// <summary>Already-ordered quantity (from existing POs/WOs).</summary>
    public decimal OrderedQty { get; set; }

    /// <summary>Available stock quantity (from Bin actual_qty).</summary>
    public decimal AvailableQty { get; set; }

    /// <summary>Final calculated quantity: MAX(0, RequiredQty - OrderedQty - AvailableQty + SafetyStock).</summary>
    public decimal PlannedQty { get; set; }

    public decimal MinOrderQty { get; set; }
    public decimal SafetyStock { get; set; }

    public string? Uom { get; set; }
    public Guid? WarehouseId { get; set; }

    /// <summary>Generated Material Request ID (populated after MR generation).</summary>
    public Guid? MaterialRequestId { get; set; }

    /// <summary>How to procure: Purchase / InHouse / Subcontract.</summary>
    public SubAssemblyType ProcurementType { get; set; }

    protected ProductionPlanMrItem() { }

    public ProductionPlanMrItem(Guid id, Guid productionPlanId, Guid itemId, string itemName, decimal requiredQty)
        : base(id)
    {
        ProductionPlanId = productionPlanId;
        ItemId = itemId;
        ItemName = itemName;
        RequiredQty = requiredQty;
    }
}
