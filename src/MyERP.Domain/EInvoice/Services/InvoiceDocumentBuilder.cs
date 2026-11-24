using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using MyERP.Tax;
using Volo.Abp.DependencyInjection;

namespace MyERP.EInvoice.Services;

/// <summary>
/// Builds UBL 2.1 Invoice XML for LHDN MyInvois submission.
/// Migrated from myinvois createxml.py — create_invoice_with_extensions() and related functions.
/// Produces XML compliant with Malaysian e-Invoice specification.
/// </summary>
public class InvoiceDocumentBuilder : ITransientDependency
{
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace InvoiceNs = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";

    /// <summary>
    /// Build UBL 2.1 XML document for LHDN submission.
    /// </summary>
    public string Build(EInvoiceDocumentData data)
    {
        var invoice = new XElement(InvoiceNs + "Invoice",
            new XAttribute(XNamespace.Xmlns + "cac", Cac.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc.NamespaceName));

        // Header
        invoice.Add(new XElement(Cbc + "ID", data.InvoiceNumber));
        invoice.Add(new XElement(Cbc + "IssueDate", data.IssueDate.ToString("yyyy-MM-dd")));
        invoice.Add(new XElement(Cbc + "IssueTime", data.IssueDate.ToString("HH:mm:ssZ")));
        invoice.Add(new XElement(Cbc + "InvoiceTypeCode",
            new XAttribute("listVersionID", string.IsNullOrWhiteSpace(data.DocumentVersion) ? "1.0" : data.DocumentVersion),
            NormalizeDocumentTypeCode(data.DocumentTypeCode)));
        invoice.Add(new XElement(Cbc + "DocumentCurrencyCode", data.CurrencyCode));
        invoice.Add(new XElement(Cbc + "TaxCurrencyCode", data.CurrencyCode));

        // Invoice Period (per myinvois createxml.py: StartDate, EndDate, Description)
        if (data.InvoicePeriodStartDate.HasValue && data.InvoicePeriodEndDate.HasValue)
        {
            var periodElem = new XElement(Cac + "InvoicePeriod",
                new XElement(Cbc + "StartDate", data.InvoicePeriodStartDate.Value.ToString("yyyy-MM-dd")),
                new XElement(Cbc + "EndDate", data.InvoicePeriodEndDate.Value.ToString("yyyy-MM-dd")));
            if (!string.IsNullOrWhiteSpace(data.InvoicePeriodDescription))
            {
                periodElem.Add(new XElement(Cbc + "Description", data.InvoicePeriodDescription));
            }
            invoice.Add(periodElem);
        }

        // Billing Reference (credit/debit/refund notes, or ICV code for standard invoices per myinvois createxml.py)
        var normalizedDocType = NormalizeDocumentTypeCode(data.DocumentTypeCode);
        if (!string.IsNullOrEmpty(data.BillingReferenceNumber) || !string.IsNullOrEmpty(data.BillingReferenceUuid))
        {
            var invoiceDocRef = new XElement(Cac + "InvoiceDocumentReference");
            if (!string.IsNullOrEmpty(data.BillingReferenceNumber))
            {
                invoiceDocRef.Add(new XElement(Cbc + "ID", data.BillingReferenceNumber));
            }
            if (!string.IsNullOrEmpty(data.BillingReferenceUuid))
            {
                invoiceDocRef.Add(new XElement(Cbc + "UUID", data.BillingReferenceUuid));
            }
            invoice.Add(new XElement(Cac + "BillingReference", invoiceDocRef));
        }
        else if (data.EmitIcvBillingReference && normalizedDocType is "01" or "11")
        {
            invoice.Add(new XElement(Cac + "BillingReference",
                new XElement(Cac + "InvoiceDocumentReference",
                    new XElement(Cbc + "ID", ExtractIcvCode(data.InvoiceNumber)))));
        }

        // Additional Document References (Customs Form 1/2/9, FTA, Incoterms — per myinvois createxml.py add_additional_document_reference)
        if (data.AdditionalDocumentReferences != null)
        {
            foreach (var docRef in data.AdditionalDocumentReferences)
            {
                var refElem = new XElement(Cac + "AdditionalDocumentReference",
                    new XElement(Cbc + "ID", docRef.Id ?? ""));
                if (!string.IsNullOrWhiteSpace(docRef.DocumentType))
                {
                    refElem.Add(new XElement(Cbc + "DocumentType", docRef.DocumentType));
                }
                if (!string.IsNullOrWhiteSpace(docRef.DocumentDescription))
                {
                    refElem.Add(new XElement(Cbc + "DocumentDescription", docRef.DocumentDescription));
                }
                invoice.Add(refElem);
            }
        }

        // Supplier (AccountingSupplierParty)
        invoice.Add(BuildSupplierParty(data.Supplier));

        // Buyer (AccountingCustomerParty)
        invoice.Add(BuildCustomerParty(data.Buyer, data.IsConsolidated));

        // Delivery (shipping address — per LHDN spec)
        if (data.Delivery != null)
        {
            invoice.Add(BuildDelivery(data.Delivery));
        }

        // Payment Means (per LHDN spec — payment mode code)
        if (data.Payment != null)
        {
            invoice.Add(BuildPaymentMeans(data.Payment));
        }

        // Payment Terms (per myinvois createxml.py payment_data)
        if (!string.IsNullOrWhiteSpace(data.PaymentTermsNote))
        {
            invoice.Add(new XElement(Cac + "PaymentTerms",
                new XElement(Cbc + "Note", data.PaymentTermsNote)));
        }

        // Prepaid Payment (per LHDN MyInvois UBL 2.1 spec)
        if (data.PrepaidPayment != null)
        {
            var prepaid = new XElement(Cac + "PrepaidPayment");
            if (!string.IsNullOrWhiteSpace(data.PrepaidPayment.Id))
                prepaid.Add(new XElement(Cbc + "ID", data.PrepaidPayment.Id));
            prepaid.Add(new XElement(Cbc + "PaidAmount",
                new XAttribute("currencyID", data.CurrencyCode),
                Math.Abs(data.PrepaidPayment.PaidAmount).ToString("F2", CultureInfo.InvariantCulture)));
            if (data.PrepaidPayment.PaidDate.HasValue)
            {
                prepaid.Add(new XElement(Cbc + "PaidDate", data.PrepaidPayment.PaidDate.Value.ToString("yyyy-MM-dd")));
                prepaid.Add(new XElement(Cbc + "PaidTime", data.PrepaidPayment.PaidDate.Value.ToString("HH:mm:ssZ")));
            }
            invoice.Add(prepaid);
        }

        // Document-level Allowance/Charge (discount)
        if (data.DiscountAmount != 0)
        {
            invoice.Add(BuildAllowanceCharge(data.DiscountAmount, data.CurrencyCode, data.DiscountReason));
        }

        // Tax Exchange Rate (mandatory per LHDN MyInvois createxml.py when document currency != MYR)
        if (!string.IsNullOrWhiteSpace(data.CurrencyCode)
            && !string.Equals(data.CurrencyCode, "MYR", StringComparison.OrdinalIgnoreCase))
        {
            var rate = data.ExchangeRate.HasValue && data.ExchangeRate.Value > 0 ? data.ExchangeRate.Value : 1m;
            invoice.Add(new XElement(Cac + "TaxExchangeRate",
                new XElement(Cbc + "SourceCurrencyCode", data.CurrencyCode),
                new XElement(Cbc + "TargetCurrencyCode", "MYR"),
                new XElement(Cbc + "CalculationRate", rate.ToString("F4", CultureInfo.InvariantCulture))));
        }

        // Tax Total
        invoice.Add(BuildTaxTotal(data.TaxAmount, data.CurrencyCode, data.TaxBreakdown));

        // Legal Monetary Total
        invoice.Add(BuildLegalMonetaryTotal(data));

        // Invoice Lines
        for (int i = 0; i < data.Lines.Count; i++)
        {
            invoice.Add(BuildInvoiceLine(i + 1, data.Lines[i], data.CurrencyCode, data.IsConsolidated));
        }

        return invoice.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// Checks whether a party contact/identifier value is blank or an N/A placeholder (<c>"NA"</c>, <c>"N/A"</c>).
    /// Per myinvois <c>createxml.py: is_na()</c>.
    /// </summary>
    public static bool IsNotApplicable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "na" or "n/a";
    }

    /// <summary>
    /// Validates basic email format before emitting <c>cbc:ElectronicMail</c> per myinvois <c>createxml.py: is_valid_email()</c>.
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var trimmed = email.Trim();
        var at = trimmed.IndexOf('@');
        if (at <= 0 || at != trimmed.LastIndexOf('@'))
            return false;

        var domain = trimmed[(at + 1)..];
        var dot = domain.LastIndexOf('.');
        return dot > 0 && dot < domain.Length - 2 && !trimmed.Contains(' ');
    }

    /// <summary>
    /// Extracts the numeric Invoice Counter Value (ICV) from an invoice number (e.g. <c>"SI-2026-00042"</c> -> <c>"00042"</c>)
    /// per myinvois <c>createxml.py</c> / <c>purchase_invoice.py: get_icv_code()</c>.
    /// </summary>
    public static string ExtractIcvCode(string? invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            return string.Empty;

        var trimmed = invoiceNumber.Trim();
        var i = trimmed.Length - 1;
        while (i >= 0 && char.IsDigit(trimmed[i]))
        {
            i--;
        }

        var trailingDigits = trimmed[(i + 1)..];
        return trailingDigits.Length > 0 ? trailingDigits : trimmed;
    }

    /// <summary>
    /// Extracts the 2-digit LHDN InvoiceTypeCode from composite strings like "02 : Credit Note" -> "02"
    /// per myinvois createxml.py get_invoice_type_code(), defaulting blank to "01".
    /// </summary>
    public static string NormalizeDocumentTypeCode(string? rawTypeCode)
    {
        if (string.IsNullOrWhiteSpace(rawTypeCode))
            return "01";

        var trimmed = rawTypeCode.Trim();
        var colonIndex = trimmed.IndexOf(':');
        if (colonIndex > 0)
        {
            trimmed = trimmed.Substring(0, colonIndex).Trim();
        }

        return string.IsNullOrWhiteSpace(trimmed) ? "01" : trimmed;
    }

    /// <summary>
    /// Extracts the LHDN state code from composite string (e.g., "14:Kuala Lumpur" -> "14")
    /// with default fallback to "17" (Not Applicable / Others) per MyInvois spec (gotcha #921).
    /// </summary>
    /// <summary>
    /// Normalizes country names and ISO-3166-1 alpha-2 codes to LHDN MyInvois ISO-3166-1 alpha-3 codes (MyInvois commit 84cf3bd).
    /// </summary>
    /// <summary>
    /// Splits composite MSIC values like "46510: Wholesale of computers" into (code, description)
    /// matching myinvois createxml.py LHDN IndustryClassificationCode serialization.
    /// </summary>
    public static (string? Code, string Description) ParseMsicCodeAndDescription(string? msicCode, string? msicDescription)
    {
        if (string.IsNullOrWhiteSpace(msicCode))
        {
            return (null, msicDescription ?? string.Empty);
        }

        var trimmed = msicCode.Trim();
        var colonIdx = trimmed.IndexOf(':');
        if (colonIdx > 0)
        {
            var codePart = trimmed[..colonIdx].Trim();
            var descPart = trimmed[(colonIdx + 1)..].Trim();
            return (codePart, !string.IsNullOrWhiteSpace(msicDescription) ? msicDescription : descPart);
        }

        return (trimmed, msicDescription ?? string.Empty);
    }

    public static string NormalizeCountryCode(string? country)
    {
        if (string.IsNullOrWhiteSpace(country)) return "MYS";
        var trimmed = country.Trim();
        return trimmed.ToUpperInvariant() switch
        {
            "MY" or "MYS" or "MALAYSIA" => "MYS",
            "SG" or "SGP" or "SINGAPORE" => "SGP",
            "US" or "USA" or "UNITED STATES" or "UNITED STATES OF AMERICA" => "USA",
            "GB" or "UK" or "GBR" or "UNITED KINGDOM" => "GBR",
            "ID" or "IDN" or "INDONESIA" => "IDN",
            "TH" or "THA" or "THAILAND" => "THA",
            "CN" or "CHN" or "CHINA" => "CHN",
            "JP" or "JPN" or "JAPAN" => "JPN",
            "IN" or "IND" or "INDIA" => "IND",
            "AU" or "AUS" or "AUSTRALIA" => "AUS",
            "BN" or "BRN" or "BRUNEI" or "BRUNEI DARUSSALAM" => "BRN",
            "VN" or "VNM" or "VIETNAM" => "VNM",
            "PH" or "PHL" or "PHILIPPINES" => "PHL",
            "DE" or "DEU" or "GERMANY" => "DEU",
            "QA" or "QAT" or "QATAR" => "QAT",
            "SA" or "SAU" or "SAUDI ARABIA" => "SAU",
            "AE" or "ARE" or "UNITED ARAB EMIRATES" or "UAE" => "ARE",
            "HK" or "HKG" or "HONG KONG" => "HKG",
            "TW" or "TWN" or "TAIWAN" => "TWN",
            "KR" or "KOR" or "SOUTH KOREA" or "KOREA, REPUBLIC OF" => "KOR",
            "NZ" or "NZL" or "NEW ZEALAND" => "NZL",
            "CA" or "CAN" or "CANADA" => "CAN",
            "FR" or "FRA" or "FRANCE" => "FRA",
            "NL" or "NLD" or "NETHERLANDS" => "NLD",
            "CH" or "CHE" or "SWITZERLAND" => "CHE",
            _ => trimmed.ToUpperInvariant()
        };
    }

    public static string NormalizeStateCode(string? state, string? countryCode = null)
    {
        if (!string.IsNullOrWhiteSpace(countryCode) && NormalizeCountryCode(countryCode) != "MYS")
        {
            return "17";
        }
        if (string.IsNullOrWhiteSpace(state)) return "17";
        var trimmed = state.Trim();
        var colonIdx = trimmed.IndexOf(':');
        if (colonIdx >= 0)
        {
            var code = trimmed.Substring(0, colonIdx).Trim();
            return string.IsNullOrEmpty(code) ? "17" : code;
        }
        return trimmed;
    }

    /// <summary>
    /// Resolves Buyer TIN and LHDN state code for Malaysian vs non-Malaysian customers
    /// per MyInvois commit d9adf36: foreign buyers default to generic TIN <c>EI00000000030</c>
    /// and state code <c>17</c> (Not Applicable), while Malaysian B2C buyers default to <c>EI00000000020</c>.
    /// </summary>
    public static (string BuyerTin, string StateCode, string CountryCode) ResolveBuyerTinAndStateForCountry(
        string? buyerTin,
        string? state,
        string? countryCode)
    {
        var normalizedCountry = NormalizeCountryCode(countryCode);
        var isMalaysian = normalizedCountry == "MYS";
        var resolvedTin = !string.IsNullOrWhiteSpace(buyerTin)
            ? buyerTin.Trim()
            : (isMalaysian ? "EI00000000020" : "EI00000000030");
        var resolvedState = isMalaysian ? NormalizeStateCode(state) : "17";
        return (resolvedTin, resolvedState, normalizedCountry);
    }

    /// <summary>
    /// Normalizes an item classification code from composite or blank MyInvois select options
    /// (e.g., <c>"003:Computer, smartphone or tablet"</c> or blank <c>""</c> per MyInvois commit e8ae07b),
    /// defaulting blank codes to <c>"022"</c> (Others) or <c>"004"</c> when consolidated.
    /// </summary>
    public static string NormalizeClassificationCode(string? classificationCode, bool isConsolidated = false)
    {
        if (isConsolidated)
        {
            return "004";
        }

        if (string.IsNullOrWhiteSpace(classificationCode))
        {
            return "022";
        }

        var trimmed = classificationCode.Trim();
        var colonIdx = trimmed.IndexOf(':');
        if (colonIdx > 0)
        {
            var code = trimmed[..colonIdx].Trim();
            return string.IsNullOrEmpty(code) ? "022" : code;
        }

        return trimmed;
    }

    /// <summary>
    /// Maps MyERP TaxType to LHDN MyInvois Tax Type Code:
    /// 01=Sales Tax, 02=Service Tax, E=Tax Exemption, 06=Not Applicable.
    /// </summary>
    public static string MapTaxTypeToLhdnCategoryCode(TaxType? taxType, decimal taxAmount)
    {
        return taxType switch
        {
            TaxType.Sales => "01",
            TaxType.Service => "02",
            TaxType.Exempt => "E",
            TaxType.ZeroRated or TaxType.OutOfScope => "06",
            // Support Credit Notes (negative taxAmount) as well as positive taxAmount
            _ => taxAmount != 0m ? "01" : "06",
        };
    }

    /// <summary>
    /// Aggregates per-line tax figures into document-level LHDN TaxSubtotal breakdowns grouped by
    /// (TaxCategoryCode, TaxRate, TaxExemptionReason).
    /// </summary>
    public static List<EInvoiceTaxBreakdown> BuildTaxBreakdown(IEnumerable<EInvoiceLineData> lines)
    {
        return lines
            .GroupBy(l => new
            {
                Category = string.IsNullOrWhiteSpace(l.TaxCategoryCode) ? (l.TaxAmount != 0m ? "01" : "06") : l.TaxCategoryCode!,
                Rate = l.TaxRate ?? 0m,
                Reason = l.TaxExemptionReason,
            })
            .Select(g => new EInvoiceTaxBreakdown
            {
                TaxCategoryCode = g.Key.Category,
                TaxRate = g.Key.Rate,
                TaxExemptionReason = g.Key.Reason,
                TaxableAmount = g.Sum(x => x.LineTotal),
                TaxAmount = g.Sum(x => x.TaxAmount),
            })
            .ToList();
    }

    /// <summary>
    /// Resolves LHDN payment mode code: Cash=01, Cheque=02, Bank Transfer=03, Credit Card=04,
    /// Debit Card=05, E-wallet=06, Others=07 per MyInvois spec (gotcha #919).
    /// </summary>
    public static string NormalizePaymentModeCode(string? paymentMode)
    {
        if (string.IsNullOrWhiteSpace(paymentMode)) return "01";
        var norm = paymentMode.Trim().ToLowerInvariant();
        if (norm.Contains("cash")) return "01";
        if (norm.Contains("cheque") || norm.Contains("check")) return "02";
        if (norm.Contains("transfer") || norm.Contains("bank") || norm.Contains("wire")) return "03";
        if (norm.Contains("credit")) return "04";
        if (norm.Contains("debit")) return "05";
        if (norm.Contains("wallet") || norm.Contains("qr") || norm.Contains("tng") || norm.Contains("grabpay") || norm.Contains("boost")) return "06";
        if (int.TryParse(norm, out var codeInt) && codeInt >= 1 && codeInt <= 7) return codeInt.ToString("D2");
        return "01";
    }

    private XElement BuildSupplierParty(EInvoicePartyData supplier)
    {
        var party = new XElement(Cac + "AccountingSupplierParty",
            new XElement(Cac + "Party",
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID",
                        new XAttribute("schemeID", "TIN"), supplier.Tin)),
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID",
                        new XAttribute("schemeID", supplier.IdType ?? "BRN"), supplier.IdValue ?? "")),
                new XElement(Cac + "PostalAddress",
                    new XElement(Cac + "AddressLine",
                        new XElement(Cbc + "Line", supplier.Address ?? "")),
                    new XElement(Cbc + "CityName", supplier.City ?? ""),
                    new XElement(Cbc + "PostalZone", supplier.PostalCode ?? ""),
                    new XElement(Cbc + "CountrySubentityCode", NormalizeStateCode(supplier.State, supplier.CountryCode)),
                    new XElement(Cac + "Country",
                        new XElement(Cbc + "IdentificationCode",
                            new XAttribute("listID", "ISO3166-1"),
                            new XAttribute("listAgencyID", "6"),
                            NormalizeCountryCode(supplier.CountryCode)))),
                new XElement(Cac + "PartyLegalEntity",
                    new XElement(Cbc + "RegistrationName", supplier.Name))));

        if (!string.IsNullOrEmpty(supplier.SstRegistration))
        {
            party.Element(Cac + "Party")!.Add(
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID",
                        new XAttribute("schemeID", "SST"), supplier.SstRegistration)));
        }

        if (!string.IsNullOrEmpty(supplier.TourismTaxNumber))
        {
            party.Element(Cac + "Party")!.Add(
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID",
                        new XAttribute("schemeID", "TTX"), supplier.TourismTaxNumber)));
        }

        // MSIC business activity code (per LHDN mandatory for supplier; supports "CODE: Description" format from MyInvois)
        var (msicCode, msicDesc) = ParseMsicCodeAndDescription(supplier.MsicCode, supplier.MsicDescription);
        if (!string.IsNullOrEmpty(msicCode))
        {
            party.Element(Cac + "Party")!.Add(
                new XElement(Cbc + "IndustryClassificationCode",
                    new XAttribute("name", msicDesc),
                    msicCode));
        }

        // Contact info (per LHDN spec & myinvois createxml.py — omit NA/invalid email)
        var hasValidPhone = !IsNotApplicable(supplier.Phone);
        var hasValidEmail = !IsNotApplicable(supplier.Email) && IsValidEmail(supplier.Email);
        if (hasValidPhone || hasValidEmail)
        {
            var contact = new XElement(Cac + "Contact");
            if (hasValidPhone)
                contact.Add(new XElement(Cbc + "Telephone", supplier.Phone!.Trim()));
            if (hasValidEmail)
                contact.Add(new XElement(Cbc + "ElectronicMail", supplier.Email!.Trim()));
            party.Element(Cac + "Party")!.Add(contact);
        }

        return party;
    }

    private XElement BuildCustomerParty(EInvoicePartyData buyer, bool isConsolidated = false)
    {
        if (isConsolidated)
        {
            var consTin = !string.IsNullOrWhiteSpace(buyer.Tin) ? buyer.Tin.Trim() : "EI00000000010";
            var consIdType = !string.IsNullOrWhiteSpace(buyer.IdType) ? buyer.IdType.Trim() : "BRN";
            var consIdValue = !string.IsNullOrWhiteSpace(buyer.IdValue) ? buyer.IdValue.Trim() : "NA";
            var consSst = !string.IsNullOrWhiteSpace(buyer.SstRegistration) ? buyer.SstRegistration.Trim() : "NA";
            var consTtx = !string.IsNullOrWhiteSpace(buyer.TourismTaxNumber) ? buyer.TourismTaxNumber.Trim() : "NA";
            var consAddress = !string.IsNullOrWhiteSpace(buyer.Address) ? buyer.Address.Trim() : "NA";
            var consName = !string.IsNullOrWhiteSpace(buyer.Name) ? buyer.Name.Trim() : "Consolidated Buyers";
            var consPhone = !string.IsNullOrWhiteSpace(buyer.Phone) ? buyer.Phone.Trim() : "NA";
            var consEmail = !string.IsNullOrWhiteSpace(buyer.Email) ? buyer.Email.Trim() : "NA";

            // Per myinvois consolidate_invoice.py (customer_data_consolidate):
            // Consolidated e-Invoices require TIN EI00000000010, BRN/SST/TTX/Address/Contact = "NA", Country = "MYS".
            return new XElement(Cac + "AccountingCustomerParty",
                new XElement(Cac + "Party",
                    new XElement(Cac + "PartyIdentification",
                        new XElement(Cbc + "ID", new XAttribute("schemeID", "TIN"), consTin)),
                    new XElement(Cac + "PartyIdentification",
                        new XElement(Cbc + "ID", new XAttribute("schemeID", consIdType), consIdValue)),
                    new XElement(Cac + "PartyIdentification",
                        new XElement(Cbc + "ID", new XAttribute("schemeID", "SST"), consSst)),
                    new XElement(Cac + "PartyIdentification",
                        new XElement(Cbc + "ID", new XAttribute("schemeID", "TTX"), consTtx)),
                    new XElement(Cac + "PostalAddress",
                        new XElement(Cac + "AddressLine",
                            new XElement(Cbc + "Line", consAddress)),
                        new XElement(Cbc + "CityName", buyer.City ?? ""),
                        new XElement(Cbc + "PostalZone", buyer.PostalCode ?? ""),
                        new XElement(Cbc + "CountrySubentityCode", NormalizeStateCode(buyer.State, "MYS")),
                        new XElement(Cac + "Country",
                            new XElement(Cbc + "IdentificationCode",
                                new XAttribute("listID", "ISO3166-1"),
                                new XAttribute("listAgencyID", "6"),
                                "MYS"))),
                    new XElement(Cac + "PartyLegalEntity",
                        new XElement(Cbc + "RegistrationName", consName)),
                    new XElement(Cac + "Contact",
                        new XElement(Cbc + "Telephone", consPhone),
                        new XElement(Cbc + "ElectronicMail", consEmail))));
        }

        var (resolvedTin, resolvedState, resolvedCountry) = ResolveBuyerTinAndStateForCountry(
            buyer.Tin, buyer.State, buyer.CountryCode);
        var party = new XElement(Cac + "AccountingCustomerParty",
            new XElement(Cac + "Party",
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID",
                        new XAttribute("schemeID", "TIN"), resolvedTin)),
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID",
                        new XAttribute("schemeID", buyer.IdType ?? "BRN"), buyer.IdValue ?? "")),
                new XElement(Cac + "PostalAddress",
                    new XElement(Cac + "AddressLine",
                        new XElement(Cbc + "Line", buyer.Address ?? "")),
                    new XElement(Cbc + "CityName", buyer.City ?? ""),
                    new XElement(Cbc + "PostalZone", buyer.PostalCode ?? ""),
                    new XElement(Cbc + "CountrySubentityCode", resolvedState),
                    new XElement(Cac + "Country",
                        new XElement(Cbc + "IdentificationCode",
                            new XAttribute("listID", "ISO3166-1"),
                            new XAttribute("listAgencyID", "6"),
                            resolvedCountry))),
                new XElement(Cac + "PartyLegalEntity",
                    new XElement(Cbc + "RegistrationName", buyer.Name))));

        if (!string.IsNullOrEmpty(buyer.SstRegistration))
        {
            party.Element(Cac + "Party")!.Add(
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID",
                        new XAttribute("schemeID", "SST"), buyer.SstRegistration)));
        }

        if (!string.IsNullOrEmpty(buyer.TourismTaxNumber))
        {
            party.Element(Cac + "Party")!.Add(
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID",
                        new XAttribute("schemeID", "TTX"), buyer.TourismTaxNumber)));
        }

        // Contact info (per LHDN spec & myinvois createxml.py — omit NA/invalid email for non-consolidated buyer)
        var hasValidBuyerPhone = !IsNotApplicable(buyer.Phone);
        var hasValidBuyerEmail = !IsNotApplicable(buyer.Email) && IsValidEmail(buyer.Email);
        if (hasValidBuyerPhone || hasValidBuyerEmail)
        {
            var contact = new XElement(Cac + "Contact");
            if (hasValidBuyerPhone)
                contact.Add(new XElement(Cbc + "Telephone", buyer.Phone!.Trim()));
            if (hasValidBuyerEmail)
                contact.Add(new XElement(Cbc + "ElectronicMail", buyer.Email!.Trim()));
            party.Element(Cac + "Party")!.Add(contact);
        }

        return party;
    }

    private XElement BuildTaxTotal(decimal taxAmount, string currency, List<EInvoiceTaxBreakdown>? breakdowns)
    {
        // Per myinvois createxml.py (tax_total / tax_total_with_template): LHDN UBL 2.1 requires
        // non-negative absolute amounts even on Credit Notes (02/12) and Refund Notes (04/14).
        var taxTotal = new XElement(Cac + "TaxTotal",
            new XElement(Cbc + "TaxAmount",
                new XAttribute("currencyID", currency),
                Math.Abs(taxAmount).ToString("F2", CultureInfo.InvariantCulture)));

        if (breakdowns != null)
        {
            foreach (var bd in breakdowns)
            {
                var taxCategory = new XElement(Cac + "TaxCategory",
                    new XElement(Cbc + "ID", bd.TaxCategoryCode),
                    new XElement(Cbc + "Percent", Math.Abs(bd.TaxRate).ToString("F2", CultureInfo.InvariantCulture)));
                if (string.Equals(bd.TaxCategoryCode, "E", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(bd.TaxExemptionReason))
                {
                    taxCategory.Add(new XElement(Cbc + "TaxExemptionReason",
                        !string.IsNullOrWhiteSpace(bd.TaxExemptionReason) ? bd.TaxExemptionReason : "NA"));
                }
                taxCategory.Add(new XElement(Cac + "TaxScheme",
                    new XElement(Cbc + "ID",
                        new XAttribute("schemeAgencyID", "6"),
                        new XAttribute("schemeID", "UN/ECE 5153"), "OTH")));

                taxTotal.Add(new XElement(Cac + "TaxSubtotal",
                    new XElement(Cbc + "TaxableAmount",
                        new XAttribute("currencyID", currency),
                        Math.Abs(bd.TaxableAmount).ToString("F2", CultureInfo.InvariantCulture)),
                    new XElement(Cbc + "TaxAmount",
                        new XAttribute("currencyID", currency),
                        Math.Abs(bd.TaxAmount).ToString("F2", CultureInfo.InvariantCulture)),
                    taxCategory));
            }
        }

        return taxTotal;
    }

    private XElement BuildLegalMonetaryTotal(EInvoiceDocumentData data)
    {
        var elem = new XElement(Cac + "LegalMonetaryTotal",
            new XElement(Cbc + "LineExtensionAmount",
                new XAttribute("currencyID", data.CurrencyCode),
                Math.Abs(data.NetTotal).ToString("F2", CultureInfo.InvariantCulture)),
            new XElement(Cbc + "TaxExclusiveAmount",
                new XAttribute("currencyID", data.CurrencyCode),
                Math.Abs(data.NetTotal).ToString("F2", CultureInfo.InvariantCulture)),
            new XElement(Cbc + "TaxInclusiveAmount",
                new XAttribute("currencyID", data.CurrencyCode),
                Math.Abs(data.GrandTotal).ToString("F2", CultureInfo.InvariantCulture)));

        if (data.DiscountAmount != 0)
        {
            elem.Add(new XElement(Cbc + "AllowanceTotalAmount",
                new XAttribute("currencyID", data.CurrencyCode),
                Math.Abs(data.DiscountAmount).ToString("F2", CultureInfo.InvariantCulture)));
        }

        if (data.ChargeTotalAmount.HasValue && data.ChargeTotalAmount.Value != 0m)
        {
            elem.Add(new XElement(Cbc + "ChargeTotalAmount",
                new XAttribute("currencyID", data.CurrencyCode),
                Math.Abs(data.ChargeTotalAmount.Value).ToString("F2", CultureInfo.InvariantCulture)));
        }

        if (data.PrepaidPayment != null && data.PrepaidPayment.PaidAmount != 0m)
        {
            elem.Add(new XElement(Cbc + "PrepaidAmount",
                new XAttribute("currencyID", data.CurrencyCode),
                Math.Abs(data.PrepaidPayment.PaidAmount).ToString("F2", CultureInfo.InvariantCulture)));
        }

        if (data.PayableRoundingAmount.HasValue && data.PayableRoundingAmount.Value != 0m)
        {
            elem.Add(new XElement(Cbc + "PayableRoundingAmount",
                new XAttribute("currencyID", data.CurrencyCode),
                data.PayableRoundingAmount.Value.ToString("F2", CultureInfo.InvariantCulture)));
        }

        var payableAmount = data.PrepaidPayment != null
            ? Math.Max(0m, Math.Abs(data.GrandTotal) - Math.Abs(data.PrepaidPayment.PaidAmount))
            : Math.Abs(data.GrandTotal);

        elem.Add(new XElement(Cbc + "PayableAmount",
            new XAttribute("currencyID", data.CurrencyCode),
            payableAmount.ToString("F2", CultureInfo.InvariantCulture)));

        return elem;
    }

    private XElement BuildInvoiceLine(int lineNumber, EInvoiceLineData line, string currency, bool isConsolidated = false)
    {
        var lineElem = new XElement(Cac + "InvoiceLine",
            new XElement(Cbc + "ID", lineNumber.ToString()),
            new XElement(Cbc + "InvoicedQuantity",
                new XAttribute("unitCode", line.Uom),
                Math.Abs(line.Quantity).ToString("F4", CultureInfo.InvariantCulture)),
            new XElement(Cbc + "LineExtensionAmount",
                new XAttribute("currencyID", currency),
                Math.Abs(line.LineTotal).ToString("F2", CultureInfo.InvariantCulture)));

        // Per-line discount (AllowanceCharge)
        if (line.DiscountAmount != 0)
        {
            lineElem.Add(new XElement(Cac + "AllowanceCharge",
                new XElement(Cbc + "ChargeIndicator", "false"),
                new XElement(Cbc + "AllowanceChargeReason", line.DiscountReason ?? "Discount"),
                new XElement(Cbc + "Amount",
                    new XAttribute("currencyID", currency),
                    Math.Abs(line.DiscountAmount).ToString("F2", CultureInfo.InvariantCulture))));
        }

        // Per-line tax
        var taxTotal = new XElement(Cac + "TaxTotal",
            new XElement(Cbc + "TaxAmount",
                new XAttribute("currencyID", currency),
                Math.Abs(line.TaxAmount).ToString("F2", CultureInfo.InvariantCulture)));
        if (line.TaxCategoryCode != null)
        {
            var lineTaxCategory = new XElement(Cac + "TaxCategory",
                new XElement(Cbc + "ID", line.TaxCategoryCode),
                new XElement(Cbc + "Percent", Math.Abs(line.TaxRate ?? 0).ToString("F2", CultureInfo.InvariantCulture)));
            if (string.Equals(line.TaxCategoryCode, "E", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(line.TaxExemptionReason))
            {
                lineTaxCategory.Add(new XElement(Cbc + "TaxExemptionReason",
                    !string.IsNullOrWhiteSpace(line.TaxExemptionReason) ? line.TaxExemptionReason : "NA"));
            }
            lineTaxCategory.Add(new XElement(Cac + "TaxScheme",
                new XElement(Cbc + "ID",
                    new XAttribute("schemeAgencyID", "6"),
                    new XAttribute("schemeID", "UN/ECE 5153"), "OTH")));

            taxTotal.Add(new XElement(Cac + "TaxSubtotal",
                new XElement(Cbc + "TaxableAmount",
                    new XAttribute("currencyID", currency),
                    Math.Abs(line.LineTotal).ToString("F2", CultureInfo.InvariantCulture)),
                new XElement(Cbc + "TaxAmount",
                    new XAttribute("currencyID", currency),
                    Math.Abs(line.TaxAmount).ToString("F2", CultureInfo.InvariantCulture)),
                lineTaxCategory));
        }
        lineElem.Add(taxTotal);

        // Item with normalized LHDN classification code (defaults blank to "022" or "004" for consolidated per MyInvois commit e8ae07b)
        var item = new XElement(Cac + "Item",
            new XElement(Cbc + "Description", line.Description),
            new XElement(Cac + "CommodityClassification",
                new XElement(Cbc + "ItemClassificationCode",
                    new XAttribute("listID", "CLASS"),
                    NormalizeClassificationCode(line.ClassificationCode, isConsolidated))));
        lineElem.Add(item);

        lineElem.Add(new XElement(Cac + "Price",
            new XElement(Cbc + "PriceAmount",
                new XAttribute("currencyID", currency),
                Math.Abs(line.UnitPrice).ToString("F4", CultureInfo.InvariantCulture))));

        lineElem.Add(new XElement(Cac + "ItemPriceExtension",
            new XElement(Cbc + "Amount",
                new XAttribute("currencyID", currency),
                Math.Abs(line.LineTotal).ToString("F2", CultureInfo.InvariantCulture))));

        return lineElem;
    }

    private XElement BuildDelivery(EInvoiceDeliveryData delivery)
    {
        var deliveryParty = new XElement(Cac + "DeliveryParty");

        // Per myinvois purchase_invoice.py & consolidate_invoice.py: emit DeliveryParty PartyIdentification (TIN, ID) when supplied
        if (!string.IsNullOrWhiteSpace(delivery.Tin))
        {
            deliveryParty.Add(new XElement(Cac + "PartyIdentification",
                new XElement(Cbc + "ID", new XAttribute("schemeID", "TIN"), delivery.Tin)));
        }

        if (!string.IsNullOrWhiteSpace(delivery.IdType) && !string.IsNullOrWhiteSpace(delivery.IdValue))
        {
            deliveryParty.Add(new XElement(Cac + "PartyIdentification",
                new XElement(Cbc + "ID", new XAttribute("schemeID", delivery.IdType), delivery.IdValue)));
        }

        deliveryParty.Add(new XElement(Cac + "PartyLegalEntity",
            new XElement(Cbc + "RegistrationName", delivery.RecipientName ?? "")));

        return new XElement(Cac + "Delivery",
            new XElement(Cac + "DeliveryAddress",
                new XElement(Cac + "AddressLine",
                    new XElement(Cbc + "Line", delivery.Address ?? "")),
                new XElement(Cbc + "CityName", delivery.City ?? ""),
                new XElement(Cbc + "PostalZone", delivery.PostalCode ?? ""),
                new XElement(Cbc + "CountrySubentityCode", NormalizeStateCode(delivery.State, delivery.CountryCode)),
                new XElement(Cac + "Country",
                    new XElement(Cbc + "IdentificationCode",
                        new XAttribute("listID", "ISO3166-1"),
                        new XAttribute("listAgencyID", "6"),
                        NormalizeCountryCode(delivery.CountryCode)))),
            deliveryParty);
    }

    private XElement BuildPaymentMeans(EInvoicePaymentData payment)
    {
        var elem = new XElement(Cac + "PaymentMeans",
            new XElement(Cbc + "PaymentMeansCode", NormalizePaymentModeCode(payment.PaymentModeCode)));
        if (!string.IsNullOrEmpty(payment.PayeeFinancialAccountId))
        {
            elem.Add(new XElement(Cac + "PayeeFinancialAccount",
                new XElement(Cbc + "ID", payment.PayeeFinancialAccountId)));
        }
        return elem;
    }

    private XElement BuildAllowanceCharge(decimal amount, string currency, string? reason)
    {
        return new XElement(Cac + "AllowanceCharge",
            new XElement(Cbc + "ChargeIndicator", "false"),
            new XElement(Cbc + "AllowanceChargeReason", reason ?? "Discount"),
            new XElement(Cbc + "Amount",
                new XAttribute("currencyID", currency),
                Math.Abs(amount).ToString("F2", CultureInfo.InvariantCulture)));
    }
}

// Data models for XML builder
public class EInvoiceDocumentData
{
    public string InvoiceNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime? InvoicePeriodStartDate { get; set; }
    public DateTime? InvoicePeriodEndDate { get; set; }
    public string? InvoicePeriodDescription { get; set; }
    public string DocumentTypeCode { get; set; } = "01";
    public string DocumentVersion { get; set; } = "1.0";
    public string CurrencyCode { get; set; } = "MYR";
    public decimal? ExchangeRate { get; set; }
    public bool IsConsolidated { get; set; }
    public EInvoicePartyData Supplier { get; set; } = null!;
    public EInvoicePartyData Buyer { get; set; } = null!;
    public EInvoiceDeliveryData? Delivery { get; set; }
    public EInvoicePaymentData? Payment { get; set; }
    public string? BillingReferenceNumber { get; set; }
    public string? BillingReferenceUuid { get; set; }
    public bool EmitIcvBillingReference { get; set; }
    public List<EInvoiceAdditionalDocumentReference>? AdditionalDocumentReferences { get; set; }
    public string? PaymentTermsNote { get; set; }
    public EInvoicePrepaidPaymentData? PrepaidPayment { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public decimal? ChargeTotalAmount { get; set; }
    public decimal? PayableRoundingAmount { get; set; }
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public List<EInvoiceLineData> Lines { get; set; } = new();
    public List<EInvoiceTaxBreakdown>? TaxBreakdown { get; set; }
}

public class EInvoicePartyData
{
    public string Name { get; set; } = null!;
    public string Tin { get; set; } = null!;
    public string? IdType { get; set; }
    public string? IdValue { get; set; }
    public string? SstRegistration { get; set; }
    public string? TourismTaxNumber { get; set; }
    public string? MsicCode { get; set; }
    public string? MsicDescription { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

/// <summary>Delivery/shipping address data for LHDN UBL Delivery section.</summary>
public class EInvoiceDeliveryData
{
    public string? RecipientName { get; set; }
    public string? Tin { get; set; }
    public string? IdType { get; set; }
    public string? IdValue { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
}

/// <summary>Payment means data per LHDN spec (payment mode code).</summary>
public class EInvoicePaymentData
{
    /// <summary>LHDN payment mode: 01=Cash, 02=Cheque, 03=Transfer, 04=Card, 05=eWallet, 06=Digital Banking, 07=Others.</summary>
    public string PaymentModeCode { get; set; } = "01";
    public string? PayeeFinancialAccountId { get; set; }
}

/// <summary>Additional document reference (Customs Form 1/2/9, FTA, Incoterms) per myinvois createxml.py.</summary>
public class EInvoiceAdditionalDocumentReference
{
    public string Id { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public string? DocumentDescription { get; set; }
}

public class EInvoicePrepaidPaymentData
{
    public string? Id { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime? PaidDate { get; set; }
}

public class EInvoiceLineData
{
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = "C62";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;
    public string? ClassificationCode { get; set; }
    public string? TaxCategoryCode { get; set; }
    public decimal? TaxRate { get; set; }
    public string? TaxExemptionReason { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
}

public class EInvoiceTaxBreakdown
{
    public string TaxCategoryCode { get; set; } = "01";
    public decimal TaxRate { get; set; }
    public string? TaxExemptionReason { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
}
