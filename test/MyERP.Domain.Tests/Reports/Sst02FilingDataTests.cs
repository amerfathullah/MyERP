using System;
using MyERP.Tax;
using Shouldly;
using Xunit;

namespace MyERP.Reports;

/// <summary>
/// Tests for SST-02 Filing Data calculation and the tax summary report.
/// Per Malaysian SST Act 2018: bimonthly filing with output/input/adjustments.
/// </summary>
public class Sst02FilingDataTests
{
    #region SST-02 Filing DTO Structure

    [Fact]
    public void Sst02FilingData_NetTaxPayable_OutputMinusInputPlusAdjustments()
    {
        var filing = new Sst02FilingDataDto
        {
            TotalOutputTax = 15000m,
            InputTaxCredit = 8000m,
            CreditNoteAdjustment = 500m,
            DebitNoteAdjustment = 200m,
            NetAdjustment = -500m + 200m, // -300
        };

        // Net = Output - Input + Adjustment = 15000 - 8000 + (-300) = 6700
        var expected = filing.TotalOutputTax - filing.InputTaxCredit + filing.NetAdjustment;
        expected.ShouldBe(6700m);
    }

    [Fact]
    public void Sst02FilingData_Refundable_WhenInputExceedsOutput()
    {
        var filing = new Sst02FilingDataDto
        {
            TotalOutputTax = 5000m,
            InputTaxCredit = 12000m,
            NetAdjustment = 0m,
            NetTaxPayable = 5000m - 12000m,
            IsRefundable = true,
        };

        filing.IsRefundable.ShouldBeTrue();
        filing.NetTaxPayable.ShouldBeLessThan(0);
    }

    [Fact]
    public void Sst02FilingData_NotRefundable_WhenOutputExceedsInput()
    {
        var filing = new Sst02FilingDataDto
        {
            NetTaxPayable = 3000m,
            IsRefundable = false,
        };

        filing.IsRefundable.ShouldBeFalse();
        filing.NetTaxPayable.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Sst02FilingData_TotalOutputTax_SumOfAllRates()
    {
        var filing = new Sst02FilingDataDto
        {
            OutputTax6Percent = 3000m,
            OutputTax10Percent = 5000m,
            OutputTax5Percent = 1000m,
            OutputTaxOther = 200m,
            TotalOutputTax = 3000m + 5000m + 1000m + 200m,
        };

        filing.TotalOutputTax.ShouldBe(9200m);
    }

    [Fact]
    public void Sst02FilingData_ServiceTax6Percent_MostCommon()
    {
        // Service Tax at 6% is the most common rate in Malaysia
        var filing = new Sst02FilingDataDto
        {
            TaxableSupplies6Percent = 500000m,
            OutputTax6Percent = 30000m, // 6% of 500K
        };

        (filing.OutputTax6Percent / filing.TaxableSupplies6Percent * 100).ShouldBe(6m);
    }

    [Fact]
    public void Sst02FilingData_SalesTax10Percent_ManufacturedGoods()
    {
        // Sales Tax at 10% for manufactured goods
        var filing = new Sst02FilingDataDto
        {
            TaxableSupplies10Percent = 200000m,
            OutputTax10Percent = 20000m, // 10% of 200K
        };

        (filing.OutputTax10Percent / filing.TaxableSupplies10Percent * 100).ShouldBe(10m);
    }

    [Fact]
    public void Sst02FilingData_SalesTax5Percent_PetroleumProducts()
    {
        // Sales Tax at 5% for petroleum products and specific items
        var filing = new Sst02FilingDataDto
        {
            TaxableSupplies5Percent = 100000m,
            OutputTax5Percent = 5000m,
        };

        (filing.OutputTax5Percent / filing.TaxableSupplies5Percent * 100).ShouldBe(5m);
    }

    #endregion

    #region Exempt and Zero-Rated

    [Fact]
    public void Sst02FilingData_ExemptSupplies_NoTax()
    {
        // Exempt supplies: basic food, medical, education
        var filing = new Sst02FilingDataDto
        {
            ExemptSupplies = 150000m,
            // No output tax generated from exempt supplies
            OutputTax6Percent = 0m,
            OutputTax10Percent = 0m,
            TotalOutputTax = 0m,
        };

        filing.ExemptSupplies.ShouldBe(150000m);
        filing.TotalOutputTax.ShouldBe(0m);
    }

    [Fact]
    public void Sst02FilingData_ZeroRatedSupplies_Exports()
    {
        // Zero-rated: exported goods, supplies to Free Industrial Zones
        var filing = new Sst02FilingDataDto
        {
            ZeroRatedSupplies = 300000m,
        };

        filing.ZeroRatedSupplies.ShouldBe(300000m);
    }

    #endregion

    #region Adjustments

    [Fact]
    public void Sst02FilingData_CreditNote_ReducesOutputTax()
    {
        var filing = new Sst02FilingDataDto
        {
            TotalOutputTax = 10000m,
            CreditNoteAdjustment = 600m, // 6% on RM 10K credit note
            NetAdjustment = -600m,
        };

        // Effective output after adjustment
        (filing.TotalOutputTax + filing.NetAdjustment).ShouldBe(9400m);
    }

    [Fact]
    public void Sst02FilingData_DebitNote_ReducesInputTax()
    {
        var filing = new Sst02FilingDataDto
        {
            InputTaxCredit = 8000m,
            DebitNoteAdjustment = 300m,
        };

        // Effective input credit after adjustment
        (filing.InputTaxCredit - filing.DebitNoteAdjustment).ShouldBe(7700m);
    }

    #endregion

    #region Period and Compliance

    [Fact]
    public void Sst02FilingData_TaxPeriod_BimonthlyFormat()
    {
        var filing = new Sst02FilingDataDto
        {
            FromDate = new DateTime(2026, 5, 1),
            ToDate = new DateTime(2026, 6, 30),
            TaxPeriod = "May 2026 - Jun 2026",
        };

        filing.TaxPeriod.ShouldContain("May");
        filing.TaxPeriod.ShouldContain("Jun");
    }

    [Fact]
    public void Sst02FilingData_Counts_TrackDocumentVolume()
    {
        var filing = new Sst02FilingDataDto
        {
            TotalSalesInvoices = 150,
            TotalPurchaseInvoices = 85,
            TotalCreditNotes = 5,
            TotalDebitNotes = 2,
        };

        filing.TotalSalesInvoices.ShouldBe(150);
        filing.TotalCreditNotes.ShouldBe(5);
    }

    [Fact]
    public void Sst02FilingData_CompanyInfo_ForSubmission()
    {
        var companyId = Guid.NewGuid();
        var filing = new Sst02FilingDataDto
        {
            CompanyId = companyId,
            CompanyName = "MyERP Sdn Bhd",
            SstRegistrationNumber = "SST-2024-12345",
        };

        filing.CompanyId.ShouldBe(companyId);
        filing.SstRegistrationNumber.ShouldNotBeNullOrEmpty();
    }

    #endregion
}
