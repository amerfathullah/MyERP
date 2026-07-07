namespace MyERP.Accounting;

public static class PaymentEntryConsts
{
    public const int MaxPaymentNumberLength = 50;
    public const int MaxReferenceNumberLength = 100;
    public const int MaxNoteLength = 1000;
    public const int MaxCurrencyCodeLength = 3;
    public const int MaxModeOfPaymentLength = 50;
}

public enum PaymentType
{
    Receive = 0,   // Customer paying us
    Pay = 1,       // We paying supplier
    InternalTransfer = 2
}
