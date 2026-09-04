using System;
using System.Collections.Generic;
using System.Xml.Linq;
using MyERP.EInvoice.Services;
using Xunit;

namespace MyERP.Domain.Tests.EInvoice;

public class EInvoiceXmlBuilderEnhancementTests
{
    private readonly InvoiceDocumentBuilder _builder = new();

    private EInvoiceDocumentData CreateBasicData() => new()
    {
        InvoiceNumber = "INV-001",
        IssueDate = new DateTime(2026, 8, 1, 10, 30, 0),
        DocumentTypeCode = "01",
        CurrencyCode = "MYR",
        Supplier = new EInvoicePartyData { Name = "Test Supplier", Tin = "C12345678900" },
        Buyer = new EInvoicePartyData { Name = "Test Buyer", Tin = "C98765432100" },
        NetTotal = 1000m, TaxAmount = 60m, GrandTotal = 1060m,
        Lines = new() { new EInvoiceLineData { Description = "Item A", Quantity = 2, UnitPrice = 500, TaxAmount = 60 } }
    };

    [Fact]
    public void Build_IncludesDeliverySection_WhenProvided()
    {
        var data = CreateBasicData();
        data.Delivery = new EInvoiceDeliveryData
        {
            RecipientName = "Warehouse A",
            Address = "123 Jalan Industri",
            City = "Shah Alam",
            State = "10",
            PostalCode = "40000",
            CountryCode = "MYS"
        };

        var xml = _builder.Build(data);

        Assert.Contains("DeliveryAddress", xml);
        Assert.Contains("123 Jalan Industri", xml);
        Assert.Contains("Shah Alam", xml);
        Assert.Contains("40000", xml);
        Assert.Contains("Warehouse A", xml);
    }

    [Fact]
    public void Build_ExcludesDelivery_WhenNull()
    {
        var data = CreateBasicData();
        data.Delivery = null;

        var xml = _builder.Build(data);

        Assert.DoesNotContain("DeliveryAddress", xml);
    }

    [Fact]
    public void Build_IncludesPaymentMeans_WhenProvided()
    {
        var data = CreateBasicData();
        data.Payment = new EInvoicePaymentData
        {
            PaymentModeCode = "03",
            PayeeFinancialAccountId = "1234567890"
        };

        var xml = _builder.Build(data);

        Assert.Contains("PaymentMeans", xml);
        Assert.Contains("03", xml);
        Assert.Contains("1234567890", xml);
    }

    [Fact]
    public void Build_IncludesBillingReference_ForCreditNotes()
    {
        var data = CreateBasicData();
        data.DocumentTypeCode = "02";
        data.BillingReferenceNumber = "INV-ORIG-001";

        var xml = _builder.Build(data);

        Assert.Contains("BillingReference", xml);
        Assert.Contains("INV-ORIG-001", xml);
    }

    [Fact]
    public void Build_IncludesBillingReferenceWithUuid_WhenUuidProvided()
    {
        var data = CreateBasicData();
        data.DocumentTypeCode = "02";
        data.BillingReferenceNumber = "INV-ORIG-001";
        data.BillingReferenceUuid = "12345678-abcd-ef01-2345-6789abcdef01";

        var xml = _builder.Build(data);

        Assert.Contains("BillingReference", xml);
        Assert.Contains("INV-ORIG-001", xml);
        Assert.Contains("12345678-abcd-ef01-2345-6789abcdef01", xml);
        Assert.Contains("<cbc:UUID>12345678-abcd-ef01-2345-6789abcdef01</cbc:UUID>", xml);
    }

    [Fact]
    public void Build_ExcludesBillingReference_WhenNull()
    {
        var data = CreateBasicData();
        data.BillingReferenceNumber = null;
        data.BillingReferenceUuid = null;

        var xml = _builder.Build(data);

        Assert.DoesNotContain("BillingReference", xml);
    }

    [Fact]
    public void Build_IncludesDocumentAllowanceCharge_WhenDiscount()
    {
        var data = CreateBasicData();
        data.DiscountAmount = 50m;
        data.DiscountReason = "Trade discount";

        var xml = _builder.Build(data);

        Assert.Contains("AllowanceCharge", xml);
        Assert.Contains("Trade discount", xml);
        Assert.Contains("50.00", xml);
    }

    [Fact]
    public void Build_ExcludesAllowanceCharge_WhenZeroDiscount()
    {
        var data = CreateBasicData();
        data.DiscountAmount = 0;

        var xml = _builder.Build(data);
        var doc = XElement.Parse(xml);
        var ns = XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
        // Should NOT have document-level AllowanceCharge (but line-level might exist)
        Assert.Null(doc.Element(ns + "AllowanceCharge"));
    }

    [Fact]
    public void Build_IncludesSupplierMsicCode()
    {
        var data = CreateBasicData();
        data.Supplier.MsicCode = "46510";
        data.Supplier.MsicDescription = "Wholesale of computers";

        var xml = _builder.Build(data);

        Assert.Contains("IndustryClassificationCode", xml);
        Assert.Contains("46510", xml);
        Assert.Contains("Wholesale of computers", xml);
    }

    [Fact]
    public void Build_IncludesSupplierContactInfo()
    {
        var data = CreateBasicData();
        data.Supplier.Phone = "+60312345678";
        data.Supplier.Email = "billing@supplier.com";

        var xml = _builder.Build(data);

        Assert.Contains("Telephone", xml);
        Assert.Contains("+60312345678", xml);
        Assert.Contains("ElectronicMail", xml);
        Assert.Contains("billing@supplier.com", xml);
    }

    [Fact]
    public void Build_IncludesPerLineClassificationCode()
    {
        var data = CreateBasicData();
        data.Lines[0].ClassificationCode = "001";

        var xml = _builder.Build(data);

        Assert.Contains("ItemClassificationCode", xml);
        Assert.Contains("CLASS", xml);
        Assert.Contains("001", xml);
    }

    [Fact]
    public void Build_IncludesPerLineTaxCategory()
    {
        var data = CreateBasicData();
        data.Lines[0].TaxCategoryCode = "01";
        data.Lines[0].TaxRate = 6m;

        var xml = _builder.Build(data);

        Assert.Contains("TaxSubtotal", xml);
        Assert.Contains("TaxCategory", xml);
    }

    [Fact]
    public void Build_IncludesPerLineDiscount()
    {
        var data = CreateBasicData();
        data.Lines[0].DiscountAmount = 25m;
        data.Lines[0].DiscountReason = "Bulk discount";

        var xml = _builder.Build(data);

        Assert.Contains("Bulk discount", xml);
        Assert.Contains("25.00", xml);
    }

    [Fact]
    public void DeliveryData_UsesActualCountryCode_NotHardcodedMYS()
    {
        var data = CreateBasicData();
        data.Delivery = new EInvoiceDeliveryData { CountryCode = "SGP", City = "Singapore" };

        var xml = _builder.Build(data);

        Assert.Contains("SGP", xml);
    }

    [Fact]
    public void PaymentData_DefaultModeCode_IsCash()
    {
        var payment = new EInvoicePaymentData();
        Assert.Equal("01", payment.PaymentModeCode);
    }

    [Fact]
    public void LineData_Defaults()
    {
        var line = new EInvoiceLineData();
        Assert.Equal("C62", line.Uom);
        Assert.Equal(0m, line.DiscountAmount);
        Assert.Null(line.ClassificationCode);
        Assert.Null(line.TaxCategoryCode);
        Assert.Null(line.TaxRate);
    }

    [Fact]
    public void EInvoicePartyData_NewFields_DefaultNull()
    {
        var party = new EInvoicePartyData();
        Assert.Null(party.MsicCode);
        Assert.Null(party.MsicDescription);
        Assert.Null(party.Phone);
        Assert.Null(party.Email);
    }

    [Fact]
    public void EInvoiceDocumentData_NewFields_Default()
    {
        var data = new EInvoiceDocumentData();
        Assert.Null(data.Delivery);
        Assert.Null(data.Payment);
        Assert.Null(data.BillingReferenceNumber);
        Assert.Equal(0m, data.DiscountAmount);
        Assert.Null(data.DiscountReason);
    }

    [Theory]
    [InlineData("01", "Cash")]
    [InlineData("02", "Cheque")]
    [InlineData("03", "Transfer")]
    [InlineData("04", "Card")]
    [InlineData("05", "eWallet")]
    [InlineData("06", "Digital Banking")]
    [InlineData("07", "Others")]
    public void PaymentModeCode_ValidLhdnCodes(string code, string _)
    {
        var data = CreateBasicData();
        data.Payment = new EInvoicePaymentData { PaymentModeCode = code };

        var xml = _builder.Build(data);

        Assert.Contains($"<cbc:PaymentMeansCode>{code}</cbc:PaymentMeansCode>", xml);
    }
}
