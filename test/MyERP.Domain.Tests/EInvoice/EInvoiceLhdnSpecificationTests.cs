using System;
using System.Collections.Generic;
using MyERP.EInvoice.Services;
using Xunit;

namespace MyERP.Domain.Tests.EInvoice;

/// <summary>
/// Unit tests for LHDN MyInvois specification rules:
/// - State code normalization and default fallback (Gotcha #921)
/// - Payment mode mapping heuristics (Gotcha #919)
/// - Document XML structure building
/// </summary>
public class EInvoiceLhdnSpecificationTests
{
    [Theory]
    [InlineData("14:Kuala Lumpur", "14")]
    [InlineData("14 : Wilayah Persekutuan Kuala Lumpur", "14")]
    [InlineData("01:Johor", "01")]
    [InlineData("08", "08")]
    [InlineData("", "17")]
    [InlineData(null, "17")]
    [InlineData("   ", "17")]
    public void NormalizeStateCode_ExtractsCodeOrFallsBack(string? input, string expected)
    {
        var result = InvoiceDocumentBuilder.NormalizeStateCode(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Cash", "01")]
    [InlineData("Cheque", "02")]
    [InlineData("Bank Transfer", "03")]
    [InlineData("Direct Wire Transfer", "03")]
    [InlineData("Credit Card", "04")]
    [InlineData("Debit Card", "05")]
    [InlineData("Touch n Go E-Wallet", "06")]
    [InlineData("GrabPay", "06")]
    [InlineData("Boost QR", "06")]
    [InlineData("Unknown Mode", "01")]
    public void NormalizePaymentModeCode_MapsToLhdnCode(string? input, string expected)
    {
        var result = InvoiceDocumentBuilder.NormalizePaymentModeCode(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void InvoiceDocumentBuilder_BuildsValidUblXmlWithNormalizedCodes()
    {
        var builder = new InvoiceDocumentBuilder();
        var docData = new EInvoiceDocumentData
        {
            InvoiceNumber = "INV-2026-0001",
            IssueDate = DateTime.UtcNow,
            DocumentTypeCode = "01",
            CurrencyCode = "MYR",
            Supplier = new EInvoicePartyData
            {
                Name = "Acme Corp Sdn Bhd",
                Tin = "C1234567890",
                IdType = "BRN",
                IdValue = "202001000001",
                Address = "123 Jalan Ampang",
                City = "Kuala Lumpur",
                State = "14:Kuala Lumpur",
                PostalCode = "50450",
                CountryCode = "MYS"
            },
            Buyer = new EInvoicePartyData
            {
                Name = "Customer Sdn Bhd",
                Tin = "C9876543210",
                IdType = "BRN",
                IdValue = "202101000002",
                Address = "456 Jalan Sultan Ismail",
                City = "Kuala Lumpur",
                State = "14 : Wilayah Persekutuan",
                PostalCode = "50250",
                CountryCode = "MYS"
            },
            Payment = new EInvoicePaymentData
            {
                PaymentModeCode = "Bank Transfer"
            },
            NetTotal = 100m,
            TaxAmount = 6m,
            GrandTotal = 106m,
            Lines = new List<EInvoiceLineData>
            {
                new()
                {
                    Description = "Consulting Service",
                    Quantity = 1,
                    UnitPrice = 100m,
                    TaxAmount = 6m,
                    TaxCategoryCode = "01",
                    TaxRate = 6m,
                    ClassificationCode = "001"
                }
            }
        };

        var xml = builder.Build(docData);

        Assert.NotNull(xml);
        Assert.Contains("<cbc:CountrySubentityCode>14</cbc:CountrySubentityCode>", xml);
        Assert.Contains("<cbc:PaymentMeansCode>03</cbc:PaymentMeansCode>", xml);
        Assert.Contains("<cbc:ID>INV-2026-0001</cbc:ID>", xml);
    }
}
