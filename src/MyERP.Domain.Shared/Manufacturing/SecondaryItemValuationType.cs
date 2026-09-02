namespace MyERP.Manufacturing;

/// <summary>
/// Valuation method for BOM Secondary Items (Co-Products, By-Products, Scrap).
/// Per ERPNext PR #58431 / v16:
/// - ValuationRate: values the item based on its own item valuation rate, deducting that cost from raw material cost.
/// - PercentageOfFgCost: allocates a percentage of remaining raw material cost.
/// - Manual: user specifies cost directly, deducting that cost from raw material cost.
/// </summary>
public enum SecondaryItemValuationType
{
    /// <summary>Valued based on its own item valuation rate.</summary>
    ValuationRate = 0,

    /// <summary>Allocates a percentage of the remaining raw material cost.</summary>
    PercentageOfFgCost = 1,

    /// <summary>Manual valuation specified on the row.</summary>
    Manual = 2,
}
