using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
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
            new XAttribute("listVersionID", "1.0"), data.DocumentTypeCode));
        invoice.Add(new XElement(Cbc + "DocumentCurrencyCode", data.CurrencyCode));

        // Billing Reference (for credit/debit notes referencing original invoice)
        if (!string.IsNullOrEmpty(data.BillingReferenceNumber))
        {
            invoice.Add(new XElement(Cac + "BillingReference",
                new XElement(Cac + "InvoiceDocumentReference",
                    new XElement(Cbc + "ID", data.BillingReferenceNumber))));
        }

        // Supplier (AccountingSupplierParty)
        invoice.Add(BuildSupplierParty(data.Supplier));

        // Buyer (AccountingCustomerParty)
        invoice.Add(BuildCustomerParty(data.Buyer));

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

        // Document-level Allowance/Charge (discount)
        if (data.DiscountAmount != 0)
        {
            invoice.Add(BuildAllowanceCharge(data.DiscountAmount, data.CurrencyCode, data.DiscountReason));
        }

        // Tax Total
        invoice.Add(BuildTaxTotal(data.TaxAmount, data.CurrencyCode, data.TaxBreakdown));

        // Legal Monetary Total
        invoice.Add(BuildLegalMonetaryTotal(data));

        // Invoice Lines
        for (int i = 0; i < data.Lines.Count; i++)
        {
            invoice.Add(BuildInvoiceLine(i + 1, data.Lines[i], data.CurrencyCode));
        }

        return invoice.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// Extracts the LHDN state code from composite string (e.g., "14:Kuala Lumpur" -> "14")
    /// with default fallback to "17" (Not Applicable / Others) per MyInvois spec (gotcha #921).
    /// </summary>
    public static string NormalizeStateCode(string? state)
    {
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
                    new XElement(Cbc + "CountrySubentityCode", NormalizeStateCode(supplier.State)),
                    new XElement(Cac + "Country",
                        new XElement(Cbc + "IdentificationCode", supplier.CountryCode ?? "MYS"))),
                new XElement(Cac + "PartyLegalEntity",
                    new XElement(Cbc + "RegistrationName", supplier.Name))));

        if (!string.IsNullOrEmpty(supplier.SstRegistration))
        {
            party.Element(Cac + "Party")!.Add(
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID",
                        new XAttribute("schemeID", "SST"), supplier.SstRegistration)));
        }

        // MSIC business activity code (per LHDN mandatory for supplier)
        if (!string.IsNullOrEmpty(supplier.MsicCode))
        {
            party.Element(Cac + "Party")!.Add(
                new XElement(Cbc + "IndustryClassificationCode",
                    new XAttribute("name", supplier.MsicDescription ?? ""),
                    supplier.MsicCode));
        }

        // Contact info (per LHDN spec — phone and email)
        if (!string.IsNullOrEmpty(supplier.Phone) || !string.IsNullOrEmpty(supplier.Email))
        {
            var contact = new XElement(Cac + "Contact");
            if (!string.IsNullOrEmpty(supplier.Phone))
                contact.Add(new XElement(Cbc + "Telephone", supplier.Phone));
            if (!string.IsNullOrEmpty(supplier.Email))
                contact.Add(new XElement(Cbc + "ElectronicMail", supplier.Email));
            party.Element(Cac + "Party")!.Add(contact);
        }

        return party;
    }

    private XElement BuildCustomerParty(EInvoicePartyData buyer)
    {
        return new XElement(Cac + "AccountingCustomerParty",
            new XElement(Cac + "Party",
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID",
                        new XAttribute("schemeID", "TIN"), buyer.Tin)),
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID",
                        new XAttribute("schemeID", buyer.IdType ?? "BRN"), buyer.IdValue ?? "")),
                new XElement(Cac + "PostalAddress",
                    new XElement(Cac + "AddressLine",
                        new XElement(Cbc + "Line", buyer.Address ?? "")),
                    new XElement(Cbc + "CityName", buyer.City ?? ""),
                    new XElement(Cbc + "PostalZone", buyer.PostalCode ?? ""),
                    new XElement(Cbc + "CountrySubentityCode", NormalizeStateCode(buyer.State)),
                    new XElement(Cac + "Country",
                        new XElement(Cbc + "IdentificationCode", buyer.CountryCode ?? "MYS"))),
                new XElement(Cac + "PartyLegalEntity",
                    new XElement(Cbc + "RegistrationName", buyer.Name))));
    }

    private XElement BuildTaxTotal(decimal taxAmount, string currency, List<EInvoiceTaxBreakdown>? breakdowns)
    {
        var taxTotal = new XElement(Cac + "TaxTotal",
            new XElement(Cbc + "TaxAmount",
                new XAttribute("currencyID", currency),
                taxAmount.ToString("F2", CultureInfo.InvariantCulture)));

        if (breakdowns != null)
        {
            foreach (var bd in breakdowns)
            {
                taxTotal.Add(new XElement(Cac + "TaxSubtotal",
                    new XElement(Cbc + "TaxableAmount",
                        new XAttribute("currencyID", currency),
                        bd.TaxableAmount.ToString("F2", CultureInfo.InvariantCulture)),
                    new XElement(Cbc + "TaxAmount",
                        new XAttribute("currencyID", currency),
                        bd.TaxAmount.ToString("F2", CultureInfo.InvariantCulture)),
                    new XElement(Cac + "TaxCategory",
                        new XElement(Cbc + "ID", bd.TaxCategoryCode),
                        new XElement(Cbc + "Percent", bd.TaxRate.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement(Cac + "TaxScheme",
                            new XElement(Cbc + "ID",
                                new XAttribute("schemeAgencyID", "6"),
                                new XAttribute("schemeID", "UN/ECE 5153"), "OTH")))));
            }
        }

        return taxTotal;
    }

    private XElement BuildLegalMonetaryTotal(EInvoiceDocumentData data)
    {
        return new XElement(Cac + "LegalMonetaryTotal",
            new XElement(Cbc + "LineExtensionAmount",
                new XAttribute("currencyID", data.CurrencyCode),
                data.NetTotal.ToString("F2", CultureInfo.InvariantCulture)),
            new XElement(Cbc + "TaxExclusiveAmount",
                new XAttribute("currencyID", data.CurrencyCode),
                data.NetTotal.ToString("F2", CultureInfo.InvariantCulture)),
            new XElement(Cbc + "TaxInclusiveAmount",
                new XAttribute("currencyID", data.CurrencyCode),
                data.GrandTotal.ToString("F2", CultureInfo.InvariantCulture)),
            new XElement(Cbc + "PayableAmount",
                new XAttribute("currencyID", data.CurrencyCode),
                data.GrandTotal.ToString("F2", CultureInfo.InvariantCulture)));
    }

    private XElement BuildInvoiceLine(int lineNumber, EInvoiceLineData line, string currency)
    {
        var lineElem = new XElement(Cac + "InvoiceLine",
            new XElement(Cbc + "ID", lineNumber.ToString()),
            new XElement(Cbc + "InvoicedQuantity",
                new XAttribute("unitCode", line.Uom),
                line.Quantity.ToString("F4", CultureInfo.InvariantCulture)),
            new XElement(Cbc + "LineExtensionAmount",
                new XAttribute("currencyID", currency),
                line.LineTotal.ToString("F2", CultureInfo.InvariantCulture)));

        // Per-line discount (AllowanceCharge)
        if (line.DiscountAmount != 0)
        {
            lineElem.Add(new XElement(Cac + "AllowanceCharge",
                new XElement(Cbc + "ChargeIndicator", "false"),
                new XElement(Cbc + "AllowanceChargeReason", line.DiscountReason ?? "Discount"),
                new XElement(Cbc + "Amount",
                    new XAttribute("currencyID", currency),
                    line.DiscountAmount.ToString("F2", CultureInfo.InvariantCulture))));
        }

        // Per-line tax
        var taxTotal = new XElement(Cac + "TaxTotal",
            new XElement(Cbc + "TaxAmount",
                new XAttribute("currencyID", currency),
                line.TaxAmount.ToString("F2", CultureInfo.InvariantCulture)));
        if (line.TaxCategoryCode != null)
        {
            taxTotal.Add(new XElement(Cac + "TaxSubtotal",
                new XElement(Cbc + "TaxableAmount",
                    new XAttribute("currencyID", currency),
                    line.LineTotal.ToString("F2", CultureInfo.InvariantCulture)),
                new XElement(Cbc + "TaxAmount",
                    new XAttribute("currencyID", currency),
                    line.TaxAmount.ToString("F2", CultureInfo.InvariantCulture)),
                new XElement(Cac + "TaxCategory",
                    new XElement(Cbc + "ID", line.TaxCategoryCode),
                    new XElement(Cbc + "Percent", (line.TaxRate ?? 0).ToString("F2", CultureInfo.InvariantCulture)),
                    new XElement(Cac + "TaxScheme",
                        new XElement(Cbc + "ID",
                            new XAttribute("schemeAgencyID", "6"),
                            new XAttribute("schemeID", "UN/ECE 5153"), "OTH")))));
        }
        lineElem.Add(taxTotal);

        // Item with classification code
        var item = new XElement(Cac + "Item",
            new XElement(Cbc + "Description", line.Description));
        if (!string.IsNullOrEmpty(line.ClassificationCode))
        {
            item.Add(new XElement(Cac + "CommodityClassification",
                new XElement(Cbc + "ItemClassificationCode",
                    new XAttribute("listID", "CLASS"), line.ClassificationCode)));
        }
        lineElem.Add(item);

        lineElem.Add(new XElement(Cac + "Price",
            new XElement(Cbc + "PriceAmount",
                new XAttribute("currencyID", currency),
                line.UnitPrice.ToString("F4", CultureInfo.InvariantCulture))));

        return lineElem;
    }

    private XElement BuildDelivery(EInvoiceDeliveryData delivery)
    {
        return new XElement(Cac + "Delivery",
            new XElement(Cac + "DeliveryAddress",
                new XElement(Cac + "AddressLine",
                    new XElement(Cbc + "Line", delivery.Address ?? "")),
                new XElement(Cbc + "CityName", delivery.City ?? ""),
                new XElement(Cbc + "PostalZone", delivery.PostalCode ?? ""),
                new XElement(Cbc + "CountrySubentityCode", NormalizeStateCode(delivery.State)),
                new XElement(Cac + "Country",
                    new XElement(Cbc + "IdentificationCode", delivery.CountryCode ?? "MYS"))),
            new XElement(Cac + "DeliveryParty",
                new XElement(Cac + "PartyLegalEntity",
                    new XElement(Cbc + "RegistrationName", delivery.RecipientName ?? ""))));
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
    public string DocumentTypeCode { get; set; } = "01";
    public string CurrencyCode { get; set; } = "MYR";
    public EInvoicePartyData Supplier { get; set; } = null!;
    public EInvoicePartyData Buyer { get; set; } = null!;
    public EInvoiceDeliveryData? Delivery { get; set; }
    public EInvoicePaymentData? Payment { get; set; }
    public string? BillingReferenceNumber { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
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
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
}

public class EInvoiceTaxBreakdown
{
    public string TaxCategoryCode { get; set; } = "01";
    public decimal TaxRate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
}
