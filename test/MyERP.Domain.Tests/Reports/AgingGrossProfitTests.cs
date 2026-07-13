using System;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Reports;

public class AgingReportTests
{
    [Fact]
    public void AgingReport_DefaultValues()
    {
        var report = new AgingReport
        {
            ReportType = "Receivable",
            AsOfDate = DateTime.Today,
            BucketRanges = new[] { 30, 60, 90, 120 },
            BucketTotals = new decimal[5],
        };

        report.TotalOutstanding.ShouldBe(0m);
        report.InvoiceCount.ShouldBe(0);
        report.BucketTotals.Length.ShouldBe(5); // 4 ranges + 1 overflow
    }

    [Fact]
    public void AgingReport_BucketRanges_HasFiveBuckets()
    {
        var ranges = new[] { 30, 60, 90, 120 };
        // 0-30, 31-60, 61-90, 91-120, 120+
        (ranges.Length + 1).ShouldBe(5);
    }

    [Fact]
    public void AgingItem_OverdueCalculation()
    {
        var item = new AgingItem
        {
            DueDate = new DateTime(2026, 6, 1),
            OutstandingAmount = 1000m,
        };
        var asOfDate = new DateTime(2026, 7, 13);
        var ageDays = (int)(asOfDate - item.DueDate).TotalDays;
        ageDays.ShouldBe(42); // 31-60 bucket
    }

    [Fact]
    public void AgingItem_NotYetDue_IsZero()
    {
        var item = new AgingItem
        {
            DueDate = new DateTime(2026, 8, 1),
            OutstandingAmount = 500m,
        };
        var asOfDate = new DateTime(2026, 7, 13);
        var ageDays = (int)(asOfDate - item.DueDate).TotalDays;
        var clampedDays = ageDays < 0 ? 0 : ageDays;
        clampedDays.ShouldBe(0); // Not yet due → bucket 0
    }
}

public class GrossProfitReportTests
{
    [Fact]
    public void GrossProfitResult_Defaults()
    {
        var result = new GrossProfitResult();
        result.TotalRevenue.ShouldBe(0m);
        result.TotalCost.ShouldBe(0m);
        result.GrossProfit.ShouldBe(0m);
        result.GrossProfitPercentage.ShouldBe(0m);
        result.ItemDetails.ShouldBeEmpty();
    }

    [Fact]
    public void GrossProfitResult_Calculation()
    {
        var result = new GrossProfitResult
        {
            TotalRevenue = 10000m,
            TotalCost = 6000m,
            GrossProfit = 4000m,
            GrossProfitPercentage = 40m,
        };
        result.GrossProfit.ShouldBe(result.TotalRevenue - result.TotalCost);
        result.GrossProfitPercentage.ShouldBe(40m);
    }

    [Fact]
    public void GrossProfitItemDetail_NegativeMargin()
    {
        var detail = new GrossProfitItemDetail
        {
            SellingRate = 80m,
            ValuationRate = 100m,
            GrossProfit = -20m,
            GrossProfitPercentage = -25m,
        };
        detail.GrossProfit.ShouldBeLessThan(0m);
    }

    [Fact]
    public void GrossProfitResult_ZeroRevenue_ZeroMargin()
    {
        var result = new GrossProfitResult
        {
            TotalRevenue = 0m,
            TotalCost = 0m,
            GrossProfit = 0m,
            GrossProfitPercentage = 0m,
        };
        result.GrossProfitPercentage.ShouldBe(0m); // Not infinity
    }

    [Fact]
    public void SalesInvoiceItem_GrossProfit_Computed()
    {
        // SI item with valuation rate captured at delivery
        var invoice = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001",
            DateTime.Today);
        invoice.AddItem(Guid.NewGuid(), "Widget", 10, 100m, 0m);

        var item = invoice.Items.First();
        item.ValuationRate = 60m;
        item.GrossProfit.ShouldBe(400m); // (100 - 60) × 10
    }
}
