namespace MyERP.Inventory;

public static class StockEntryConsts
{
    public const int MaxEntryNumberLength = 50;
    public const int MaxReferenceNumberLength = 50;
    public const int MaxNoteLength = 1000;
}

/// <summary>
/// Stock Entry types — all 13 standard types from ERPNext.
/// Per settings-configuration: "13 — immutable, is_standard=True"
/// </summary>
public enum StockEntryType
{
    /// <summary>Receive stock into warehouse (purchase, opening).</summary>
    MaterialReceipt = 0,

    /// <summary>Issue stock out of warehouse (consumption, write-off).</summary>
    MaterialIssue = 1,

    /// <summary>Move stock between warehouses.</summary>
    MaterialTransfer = 2,

    /// <summary>Transfer raw materials for manufacturing (WIP warehouse).</summary>
    MaterialTransferForManufacture = 3,

    /// <summary>Record finished goods production from raw materials.</summary>
    Manufacture = 4,

    /// <summary>Repack items (break down / combine).</summary>
    Repack = 5,

    /// <summary>Send raw materials to subcontractor.</summary>
    SendToSubcontractor = 6,

    /// <summary>Track actual material consumption for manufacture.</summary>
    MaterialConsumptionForManufacture = 7,

    /// <summary>Disassemble finished goods back into components.</summary>
    Disassemble = 8,

    /// <summary>Send stock to transit warehouse (inter-warehouse first leg).</summary>
    SendToWarehouse = 9,

    /// <summary>Receive stock from transit warehouse (inter-warehouse second leg).</summary>
    ReceiveAtWarehouse = 10,

    /// <summary>Deliver raw materials to subcontractor (subcontracting flow).</summary>
    SubcontractingDelivery = 11,

    /// <summary>Return materials from subcontractor.</summary>
    SubcontractingReturn = 12,

    /// <summary>Adjust stock quantity/value (reconciliation).</summary>
    Adjustment = 13
}

public enum StockMovementDirection
{
    In = 0,
    Out = 1
}
