namespace MyERP.Purchasing;

/// <summary>
/// Supplier hold types — controls which transactions are blocked.
/// ERPNext equivalent: Supplier.hold_type field.
/// </summary>
public enum SupplierHoldType
{
    /// <summary>No hold — all transactions allowed.</summary>
    None = 0,

    /// <summary>All transactions blocked (PO, PI, PE).</summary>
    All = 1,

    /// <summary>Only Purchase Invoices blocked.</summary>
    Invoices = 2,

    /// <summary>Only Payments blocked.</summary>
    Payments = 3
}
