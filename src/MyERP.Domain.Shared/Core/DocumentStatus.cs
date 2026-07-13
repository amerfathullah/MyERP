namespace MyERP.Core;

public enum DocumentStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Posted = 3,
    Cancelled = 4,
    Rejected = 5,

    // Fulfillment statuses (for SO/PO/DN/PR lifecycle)
    ToDeliverAndBill = 10,
    ToDeliver = 11,
    ToBill = 12,
    Completed = 13,
    Closed = 14
}
