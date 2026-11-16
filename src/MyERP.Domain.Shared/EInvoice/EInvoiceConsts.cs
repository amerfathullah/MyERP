using System;
using System.Collections.Generic;
using Volo.Abp;

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
    public const int MaxClassificationCodeLength = 10;
    public const int MaxClassificationInputLength = 200;

    /// <summary>Generic buyer TIN for Malaysian general public / walk-in consumers without individual TIN.</summary>
    public const string GenericBuyerTin = "EI00000000020";

    /// <summary>Generic TIN for foreign buyers and foreign suppliers (non-Malaysian) without Malaysian TIN.</summary>
    public const string ForeignPartyTin = "EI00000000030";

    /// <summary>Default LHDN MyInvois classification code for general sales items ("Others").</summary>
    public const string DefaultSalesClassificationCode = "022";

    /// <summary>Default LHDN MyInvois classification code for self-billed purchase items ("Self-billed - Others").</summary>
    public const string DefaultSelfBilledClassificationCode = "036";

    /// <summary>LHDN MyInvois classification code for consolidated B2C e-Invoices.</summary>
    public const string ConsolidatedClassificationCode = "004";
}

/// <summary>
/// Official LHDN MyInvois Item Classification Codes (001-045) per myinvois custom_field specification.
/// </summary>
public static class LhdnClassificationCodes
{
    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["001"] = "Breastfeeding equipment",
        ["002"] = "Child care centres and kindergartens fees",
        ["003"] = "Computer, smartphone or tablet",
        ["004"] = "Consolidated e-Invoice",
        ["005"] = "Construction materials (as specified under Fourth Schedule of the Lembaga Pembangunan Industri Pembinaan Malaysia Act 1994)",
        ["006"] = "Disbursement",
        ["007"] = "Donation",
        ["008"] = "e-Commerce - e-Invoice to buyer / purchaser",
        ["009"] = "e-Commerce - Self-billed e-Invoice to seller, logistics, etc.",
        ["010"] = "Education fees",
        ["011"] = "Goods on consignment (Consignor)",
        ["012"] = "Goods on consignment (Consignee)",
        ["013"] = "Gym membership",
        ["014"] = "Insurance - Education and medical benefits",
        ["015"] = "Insurance - Takaful or life insurance",
        ["016"] = "Interest and financing expenses",
        ["017"] = "Internet subscription",
        ["018"] = "Land and building",
        ["019"] = "Medical examination for learning disabilities and early intervention or rehabilitation treatments of learning disabilities",
        ["020"] = "Medical examination or vaccination expenses",
        ["021"] = "Medical expenses for serious diseases",
        ["022"] = "Others",
        ["023"] = "Petroleum operations (as defined in Petroleum (Income Tax) Act 1967)",
        ["024"] = "Private retirement scheme or deferred annuity scheme",
        ["025"] = "Motor vehicle",
        ["026"] = "Subscription of books / journals / magazines / newspapers / other similar publications",
        ["027"] = "Reimbursement",
        ["028"] = "Rental of motor vehicle",
        ["029"] = "EV charging facilities (Installation, rental, sale / purchase or subscription fees)",
        ["030"] = "Repair and maintenance",
        ["031"] = "Research and development",
        ["032"] = "Foreign income",
        ["033"] = "Self-billed - Betting and gaming",
        ["034"] = "Self-billed - Importation of goods",
        ["035"] = "Self-billed - Importation of services",
        ["036"] = "Self-billed - Others",
        ["037"] = "Self-billed - Monetary payment to agents, dealers or distributors",
        ["038"] = "Sports equipment",
        ["039"] = "Supporting equipment for disabled person",
        ["040"] = "Voluntary contribution to approved provident fund",
        ["041"] = "Dental examination or treatment",
        ["042"] = "Fertility treatment",
        ["043"] = "Treatment and home care nursing, daycare centres and residential care centers",
        ["044"] = "Vouchers, gift cards, loyalty points, etc",
        ["045"] = "Self-billed - Non-monetary payment to agents, dealers or distributors",
    };

    /// <summary>Extracts the 3-digit code from values like "022" or "022:Others"; returns null if empty.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();
        var colonIdx = trimmed.IndexOf(':');
        var code = (colonIdx >= 0 ? trimmed[..colonIdx] : trimmed).Trim();
        return string.IsNullOrEmpty(code) ? null : code;
    }

    /// <summary>Normalizes and validates a non-empty code against the LHDN 001-045 classification catalog.</summary>
    public static string? NormalizeAndValidate(string? raw)
    {
        var code = Normalize(raw);
        if (code == null)
            return null;

        if (!Descriptions.ContainsKey(code))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Invalid LHDN Item Classification Code '{code}'. Expected a valid code between '001' and '045'.");
        }

        return code;
    }
}

/// <summary>
/// LHDN MyInvois API environment.
/// </summary>
public enum LhdnEnvironment
{
    Sandbox = 0,
    Production = 1
}
