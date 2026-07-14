namespace MyERP.Sales;

/// <summary>
/// How the shipping amount is calculated.
/// </summary>
public enum ShippingCalculationMode
{
    /// <summary>Fixed shipping amount regardless of order size.</summary>
    Fixed = 0,

    /// <summary>Based on Net Total — matches condition by document net total.</summary>
    BasedOnNetTotal = 1,

    /// <summary>Based on Net Weight — matches condition by total weight of items.</summary>
    BasedOnNetWeight = 2
}

/// <summary>
/// Whether this is a selling or buying shipping rule.
/// </summary>
public enum ShippingRuleType
{
    Selling = 0,
    Buying = 1
}
