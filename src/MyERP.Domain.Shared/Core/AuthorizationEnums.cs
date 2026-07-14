namespace MyERP.Core;

/// <summary>
/// What metric an authorization rule checks against.
/// </summary>
public enum AuthorizationBasedOn
{
    /// <summary>Transaction total (base_grand_total).</summary>
    GrandTotal = 0,

    /// <summary>Average discount across all items: 100 - (base_rate_total / price_list_rate_total × 100).</summary>
    AverageDiscount = 1,

    /// <summary>Same as AverageDiscount but filtered by specific customer.</summary>
    CustomerwiseDiscount = 2,

    /// <summary>Per-item discount_percentage (checked for each item individually).</summary>
    ItemwiseDiscount = 3,

    /// <summary>Per-item discount matched by item group.</summary>
    ItemGroupWiseDiscount = 4
}
