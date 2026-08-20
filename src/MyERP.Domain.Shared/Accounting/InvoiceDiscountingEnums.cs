namespace MyERP.Accounting;

/// <summary>
/// Invoice Discounting state machine. Maps to ERPNext accounts/doctype/invoice_discounting status field.
/// Draft -&gt; Sanctioned (on submit) -&gt; Disbursed (bank pays out) -&gt; Settled (loan repaid).
/// Draft/Sanctioned/Disbursed can all be Cancelled.
/// </summary>
public enum InvoiceDiscountingStatus
{
    Draft = 0,
    Sanctioned = 1,
    Disbursed = 2,
    Settled = 3,
    Cancelled = 4,
}
