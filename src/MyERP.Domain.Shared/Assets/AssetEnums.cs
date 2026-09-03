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
    Capitalized = 8,
}

public enum DepreciationMethod
{
    StraightLine = 0,
    DoubleDecliningBalance = 1,
    WrittenDownValue = 2,
    Manual = 3,
}

public enum AssetMovementPurpose
{
    Issue = 0,
    Receipt = 1,
    Transfer = 2,
    TransferAndIssue = 3,
}

public enum AssetRepairStatus
{
    Pending = 0,
    Completed = 1,
    Cancelled = 2,
}

public enum AssetCapitalizationStatus
{
    Draft = 0,
    Submitted = 1,
    Cancelled = 2,
}

public enum AssetCapitalizationTargetType
{
    Asset = 0,
    StockItem = 1,
}

public enum AssetActivityType
{
    Created = 0,
    Depreciated = 1,
    Moved = 2,
    Repaired = 3,
    Capitalized = 4,
    Adjusted = 5,
    Scrapped = 6,
    Sold = 7,
    Restored = 8,
    Cancelled = 9,
}

