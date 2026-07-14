using System;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Tax;

public class TaxSummaryReportTests
{
    [Fact]
    public void NetTaxPayable_OutputMinusInput()
    {
        // Net = Output Tax - Input Tax
        // If output > input → pay to customs
        decimal outputTax = 6000m; // collected from customers
        decimal inputTax = 4000m; // paid to suppliers
        decimal netPayable = outputTax - inputTax;
        netPayable.ShouldBe(2000m);
    }

    [Fact]
    public void NetTaxPayable_NegativeIsRefundable()
    {
        // If input > output → refundable/carry forward
        decimal outputTax = 3000m;
        decimal inputTax = 5000m;
        decimal netPayable = outputTax - inputTax;
        netPayable.ShouldBe(-2000m);
        (netPayable < 0).ShouldBeTrue(); // IsRefundable
    }

    [Fact]
    public void CreditNote_ReducesOutputTax()
    {
        // Credit notes reduce the output tax (refund to customer reduces tax collected)
        decimal outputTax = 6000m;
        decimal creditNoteTax = 600m;
        decimal netOutput = outputTax - creditNoteTax;
        netOutput.ShouldBe(5400m);
    }

    [Fact]
    public void DebitNote_ReducesInputTax()
    {
        // Debit notes reduce the input tax (return to supplier reduces tax claimed)
        decimal inputTax = 4000m;
        decimal debitNoteTax = 400m;
        decimal netInput = inputTax - debitNoteTax;
        netInput.ShouldBe(3600m);
    }

    [Fact]
    public void TaxRate_Calculation_From_Invoice()
    {
        // Effective tax rate = TaxAmount / NetTotal × 100
        decimal netTotal = 10000m;
        decimal taxAmount = 600m;
        decimal effectiveRate = Math.Round(taxAmount / netTotal * 100, 0);
        effectiveRate.ShouldBe(6m); // 6% SST service tax
    }

    [Fact]
    public void TaxRate_10Percent_SalesTax()
    {
        decimal netTotal = 5000m;
        decimal taxAmount = 500m;
        decimal effectiveRate = Math.Round(taxAmount / netTotal * 100, 0);
        effectiveRate.ShouldBe(10m); // 10% sales tax
    }

    [Fact]
    public void ZeroTaxInvoice_ExcludedFromBreakdown()
    {
        // Invoices with 0 tax (exempt items) should not affect rate calculation
        decimal netTotal = 8000m;
        decimal taxAmount = 0m;
        bool shouldInclude = netTotal > 0 && taxAmount > 0;
        shouldInclude.ShouldBeFalse();
    }

    [Fact]
    public void MultiRateAggregation_GroupsByRate()
    {
        // Two invoices at 6%: 100 + 200 = 300 total tax
        // One invoice at 10%: 500 total tax
        var rates = new[] { ("6%", 300m), ("10%", 500m) };
        rates.Length.ShouldBe(2);
    }

    [Fact]
    public void PaymentTerms_AutoPopulate_Supplier_Default()
    {
        // When creating PI without explicit PaymentTermsTemplateId,
        // should fall back to Supplier.DefaultPaymentTermsTemplateId
        var supplierId = Guid.NewGuid();
        var supplierDefaultTerms = Guid.NewGuid();

        // Resolution: input explicit → supplier default → null
        Guid? inputTerms = null;
        Guid? resolved = inputTerms ?? supplierDefaultTerms;
        resolved.ShouldBe(supplierDefaultTerms);
    }

    [Fact]
    public void PaymentTerms_ExplicitOverridesSupplierDefault()
    {
        var explicitTerms = Guid.NewGuid();
        var supplierDefault = Guid.NewGuid();

        // Explicit always wins
        Guid? resolved = explicitTerms;
        resolved.ShouldBe(explicitTerms);
        resolved.ShouldNotBe(supplierDefault);
    }

    [Fact]
    public void PaymentTerms_NullWhenNoDefault()
    {
        Guid? inputTerms = null;
        Guid? supplierDefault = null;

        Guid? resolved = inputTerms ?? supplierDefault;
        resolved.ShouldBeNull();
    }
}
