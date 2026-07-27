namespace MyERP.Sales;

public static class SalesPartnerConsts
{
    public const int MaxNameLength = 200;
    public const int MaxPartnerTypeLength = 50;
    public const int MaxTerritoryLength = 100;
    public const int MaxWebsiteLength = 500;
}

/// <summary>Sales partner type classification.</summary>
public enum PartnerType
{
    Reseller = 0,
    Distributor = 1,
    Dealer = 2,
    Agent = 3,
    Broker = 4,
    Referral = 5
}
