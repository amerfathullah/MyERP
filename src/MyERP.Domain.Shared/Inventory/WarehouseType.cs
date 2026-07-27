namespace MyERP.Inventory;

/// <summary>
/// Warehouse type classification per ERPNext.
/// Transit warehouses are used for inter-company/inter-warehouse transfers.
/// </summary>
public enum WarehouseType
{
    /// <summary>Standard warehouse for stock storage.</summary>
    Standard = 0,

    /// <summary>Transit warehouse for stock in transit between warehouses/companies.</summary>
    Transit = 1,

    /// <summary>Rejected goods warehouse for QI-failed items.</summary>
    Rejected = 2,

    /// <summary>Sample retention warehouse for QC samples.</summary>
    SampleRetention = 3
}
