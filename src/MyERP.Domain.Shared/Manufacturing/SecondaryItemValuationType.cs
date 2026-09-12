namespace MyERP.Manufacturing;

/// <summary>
/// Valuation method for BOM Secondary Items (Co-Products, By-Products, Scrap).
/// Per ERPNext PR #58431 / PR #59021 (v16):
/// - ValuationRate: values the item based on its own item valuation rate, deducting that cost from raw material cost.
/// - PercentageOfComponentCost: allocates a percentage of remaining raw material (component) cost (renamed from % of FG Cost in PR #59021).
/// - Manual: user specifies cost directly, deducting that cost from raw material cost.
/// </summary>
public enum SecondaryItemValuationType
{
    /// <summary>Valued based on its own item valuation rate.</summary>
    ValuationRate = 0,

    /// <summary>Allocates a percentage of the remaining raw material (component) cost (PR #59021).</summary>
    PercentageOfComponentCost = 1,

    /// <summary>Backward-compatible alias for PercentageOfComponentCost.</summary>
    [System.Obsolete("Renamed in ERPNext PR #59021 to PercentageOfComponentCost.")]
    PercentageOfFgCost = 1,

    /// <summary>Manual valuation specified on the row.</summary>
    Manual = 2,
}
