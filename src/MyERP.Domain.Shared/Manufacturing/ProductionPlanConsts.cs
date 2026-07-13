namespace MyERP.Manufacturing;

public static class ProductionPlanConsts
{
    public const int MaxPlanNumberLength = 50;
    public const int MaxNoteLength = 2000;
    public const int MaxItemNameLength = 200;
    public const int MaxUomLength = 20;
    public const int MaxWarehouseNameLength = 200;
}

public enum ProductionPlanStatus
{
    Draft = 0,
    Submitted = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
}

public enum SubAssemblyType
{
    InHouseManufacturing = 0,
    Subcontracting = 1,
    MaterialRequest = 2,
}
