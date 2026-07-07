namespace MyERP.Sales;

/// <summary>
/// e-Invoice document type codes per LHDN MyInvois specification.
/// </summary>
public enum EInvoiceDocumentType
{
    Invoice = 1,
    CreditNote = 2,
    DebitNote = 3,
    RefundNote = 4,
    SelfBilledInvoice = 11,
    SelfBilledCreditNote = 12,
    SelfBilledDebitNote = 13,
    SelfBilledRefundNote = 14
}

public enum EInvoiceStatus
{
    NotSubmitted = 0,
    Pending = 1,
    Valid = 2,
    Invalid = 3,
    Cancelled = 4,
    Rejected = 5
}
