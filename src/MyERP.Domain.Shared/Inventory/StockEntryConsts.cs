namespace MyERP.Inventory;

public static class StockEntryConsts
{
    public const int MaxEntryNumberLength = 50;
    public const int MaxReferenceNumberLength = 50;
    public const int MaxNoteLength = 1000;
}

public enum StockEntryType
{
    Receipt = 0,
    Issue = 1,
    Transfer = 2,
    Adjustment = 3
}

public enum StockMovementDirection
{
    In = 0,
    Out = 1
}
