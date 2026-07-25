namespace MyERP.Sales;

/// <summary>
/// Proforma Invoice basis — determines what the user edits per line.
/// Per ERPNext PR #57263.
/// </summary>
public enum ProformaInvoiceBasis
{
    /// <summary>Rate is fixed from SO; user enters qty. Amount = qty × rate.</summary>
    Quantity = 0,

    /// <summary>User enters both qty and amount; rate is derived (amount / qty).</summary>
    Amount = 1
}
