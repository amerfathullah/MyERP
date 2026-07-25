namespace MyERP.Sales;

/// <summary>
/// Proforma Invoice status (v16 feature — progressive/partial invoicing before delivery).
/// Per ERPNext PR #57263.
/// </summary>
public enum ProformaInvoiceStatus
{
    Draft = 0,
    Issued = 1,
    Cancelled = 2
}
