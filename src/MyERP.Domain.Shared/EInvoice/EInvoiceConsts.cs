namespace MyERP.EInvoice;

public static class EInvoiceConsts
{
    public const int MaxUuidLength = 50;
    public const int MaxLongIdLength = 100;
    public const int MaxSubmissionUidLength = 50;
    public const int MaxDocumentTypeLength = 5;
    public const int MaxStatusLength = 20;
    public const int MaxReasonLength = 500;
    public const int MaxQrCodeUrlLength = 512;

    /// <summary>Generic buyer TIN for Malaysian general public / walk-in consumers without individual TIN.</summary>
    public const string GenericBuyerTin = "EI00000000020";

    /// <summary>Generic TIN for foreign buyers and foreign suppliers (non-Malaysian) without Malaysian TIN.</summary>
    public const string ForeignPartyTin = "EI00000000030";
}

/// <summary>
/// LHDN MyInvois API environment.
/// </summary>
public enum LhdnEnvironment
{
    Sandbox = 0,
    Production = 1
}
