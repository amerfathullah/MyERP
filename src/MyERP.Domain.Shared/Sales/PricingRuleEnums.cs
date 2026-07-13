namespace MyERP.Sales;

/// <summary>What the pricing rule applies to.</summary>
public enum PricingRuleApplyOn
{
    ItemCode = 0,
    ItemGroup = 1,
    Brand = 2,
    TransactionTotal = 3
}

/// <summary>Type of discount the pricing rule provides.</summary>
public enum PricingRuleType
{
    Discount = 0,
    Rate = 1,
    FreeItem = 2
}
