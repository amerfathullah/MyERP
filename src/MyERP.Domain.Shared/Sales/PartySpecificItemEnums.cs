namespace MyERP.Sales;

/// <summary>Party type an item-visibility restriction applies to.</summary>
public enum PartySpecificItemPartyType
{
    Customer = 0,
    CustomerGroup = 1,
    Supplier = 2,
    SupplierGroup = 3
}

/// <summary>Basis on which the item filter restricts visibility.</summary>
public enum PartySpecificItemRestrictBasedOn
{
    Item = 0,
    ItemGroup = 1,
    Brand = 2
}
