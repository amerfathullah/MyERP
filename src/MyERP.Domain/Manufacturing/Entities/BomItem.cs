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

    /// <summary>Stock UOM from Item master (denormalized for SLE creation).</summary>
    public string StockUom { get; set; } = "Unit";

    /// <summary>Conversion factor: transaction UOM → stock UOM.</summary>
    public decimal ConversionFactor { get; set; } = 1m;

    /// <summary>Quantity in stock UOM = Quantity × ConversionFactor.</summary>
    public decimal StockQty => Quantity * ConversionFactor;

    /// <summary>
    /// If this item is a sub-assembly, references its BOM for recursive explosion.
    /// </summary>
    public Guid? SubBomId { get; set; }

    /// <summary>
    /// Phantom items are not produced independently — their components bubble up to the parent BOM.
    /// </summary>
    public bool IsPhantom { get; set; }

    /// <summary>Percentage formulation when BOM.SetQtyBasedOnPercentage is active (ERPNext commit d07f4bb857).</summary>
    public decimal Percentage { get; set; }

    /// <summary>Absorbs the remaining percentage to make components sum to 100%.</summary>
    public bool IsBalanceItem { get; set; }

    protected BomItem() { }

    public BomItem(Guid id, Guid bomId, Guid itemId, string itemName, decimal quantity, decimal rate,
        string? uom = null, decimal conversionFactor = 1m, string stockUom = "Unit")
        : base(id)
    {
        BomId = bomId;
        ItemId = itemId;
        ItemName = itemName;
        Quantity = quantity;
        Rate = rate;
        Uom = uom;
        ConversionFactor = conversionFactor;
        StockUom = stockUom;
        Amount = quantity * rate;
    }

    /// <summary>
    /// Per PR #57708: amount is per-row (qty × rate), not aggregated across duplicate items.
    /// Rate is in transaction UOM; for stock-UOM-based amount, use StockQty × (Rate / ConversionFactor).
    /// </summary>
    public void Recalculate()
    {
        Amount = Quantity * Rate;
    }
}
