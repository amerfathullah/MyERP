namespace MyERP.Accounting;

/// <summary>What a Payment Order batches: pending Payment Requests, or already-created Payment Entries awaiting bank submission.</summary>
public enum PaymentOrderType
{
    PaymentRequest = 0,
    PaymentEntry = 1,
}

public static class PaymentOrderConsts
{
    public const int MaxModeOfPaymentLength = 50;
    public const int MaxReferenceTypeLength = 50;
    public const int MaxPaymentReferenceLength = 100;
}

/// <summary>Voucher types Unreconcile Payment can unlink allocations from.</summary>
public enum UnreconcileVoucherType
{
    PaymentEntry = 0,
    JournalEntry = 1,
}
