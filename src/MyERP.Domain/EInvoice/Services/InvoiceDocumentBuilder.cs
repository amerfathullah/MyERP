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

        // Supplier (AccountingSupplierParty)
        invoice.Add(BuildSupplierParty(data.Supplier));

        // Buyer (AccountingCustomerParty)
        invoice.Add(BuildCustomerParty(data.Buyer));

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
                    new XElement(Cbc + "CountrySubentityCode", supplier.State ?? ""),
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
                    new XElement(Cbc + "CountrySubentityCode", buyer.State ?? ""),
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
        return new XElement(Cac + "InvoiceLine",
            new XElement(Cbc + "ID", lineNumber.ToString()),
            new XElement(Cbc + "InvoicedQuantity",
                new XAttribute("unitCode", line.Uom),
                line.Quantity.ToString("F4", CultureInfo.InvariantCulture)),
            new XElement(Cbc + "LineExtensionAmount",
                new XAttribute("currencyID", currency),
                line.LineTotal.ToString("F2", CultureInfo.InvariantCulture)),
            new XElement(Cac + "TaxTotal",
                new XElement(Cbc + "TaxAmount",
                    new XAttribute("currencyID", currency),
                    line.TaxAmount.ToString("F2", CultureInfo.InvariantCulture))),
            new XElement(Cac + "Item",
                new XElement(Cbc + "Description", line.Description)),
            new XElement(Cac + "Price",
                new XElement(Cbc + "PriceAmount",
                    new XAttribute("currencyID", currency),
                    line.UnitPrice.ToString("F4", CultureInfo.InvariantCulture))));
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
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
}

public class EInvoiceLineData
{
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = "C62";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;
}

public class EInvoiceTaxBreakdown
{
    public string TaxCategoryCode { get; set; } = "01";
    public decimal TaxRate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
}
