namespace MyERP.Assets;

public enum AssetStatus
{
    Draft = 0,
    Submitted = 1,
    PartiallyDepreciated = 2,
    FullyDepreciated = 3,
    Sold = 4,
    Scrapped = 5,
    InMaintenance = 6,
    Cancelled = 7,
}

public enum DepreciationMethod
{
    StraightLine = 0,
    DoubleDecliningBalance = 1,
    WrittenDownValue = 2,
    Manual = 3,
}
