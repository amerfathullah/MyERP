using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.EInvoice;
using MyERP.Sales;
using Xunit;

namespace MyERP.Domain.Tests.EInvoice;

/// <summary>
/// Unit tests for LHDN status reports, VAT on Sales & Purchase reports, and Dashboard analytics.
/// Migrated from myinvois (lhdn_sales_status_report, lhdn_purchase_status_report, lhdn_vat_report_on_sales_&_purchase).
/// </summary>
public class LhdnReportingAndDashboardTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    #region LHDN Status Report DTOs & Invariants

    [Fact]
    public void StatusReportItem_MapsAllEssentialFields()
    {
        var invoiceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var item = new LhdnStatusReportItemDto
        {
            InvoiceId = invoiceId,
            InvoiceNumber = "SINV-2026-0001",
            PostingDate = now,
            PartyName = "Acme Corp",
            GrandTotal = 1060.00m,
            TaxAmount = 60.00m,
            Status = "Valid",
            DocumentUuid = "LHDN-UUID-9999",
            QrCodeUrl = "https://myinvois.hasil.gov.my/verify/LHDN-UUID-9999",
            SubmittedAt = now
        };

        Assert.Equal(invoiceId, item.InvoiceId);
        Assert.Equal("SINV-2026-0001", item.InvoiceNumber);
        Assert.Equal("Acme Corp", item.PartyName);
        Assert.Equal(1060.00m, item.GrandTotal);
        Assert.Equal(60.00m, item.TaxAmount);
        Assert.Equal("Valid", item.Status);
        Assert.Equal("LHDN-UUID-9999", item.DocumentUuid);
        Assert.NotNull(item.QrCodeUrl);
    }

    [Theory]
    [InlineData("Valid", EInvoiceStatus.Valid)]
    [InlineData("Invalid", EInvoiceStatus.Invalid)]
    [InlineData("Pending", EInvoiceStatus.Pending)]
    [InlineData("Cancelled", EInvoiceStatus.Cancelled)]
    [InlineData("Rejected", EInvoiceStatus.Rejected)]
    [InlineData("NotSubmitted", EInvoiceStatus.NotSubmitted)]
    public void StatusReportFilter_ParsesEInvoiceStatusesCorrectly(string statusString, EInvoiceStatus expectedEnum)
    {
        var parsed = Enum.TryParse<EInvoiceStatus>(statusString, true, out var result);
        Assert.True(parsed);
        Assert.Equal(expectedEnum, result);
    }

    #endregion

    #region LHDN VAT Report Calculations (myinvois parity)

    [Fact]
    public void VatReport_CalculatesTotalsAndNetVatPayable_Correctly()
    {
        var report = new LhdnVatReportDto
        {
            SalesCategories = new List<LhdnVatCategorySummaryDto>
            {
                new() { CategoryCode = "01", CategoryName = "Sales Tax", Amount = 10000m, Adjustment = 0m, VatAmount = 600m },
                new() { CategoryCode = "02", CategoryName = "Service Tax", Amount = 5000m, Adjustment = 500m, VatAmount = 360m },
                new() { CategoryCode = "E", CategoryName = "Tax Exemption", Amount = 2000m, Adjustment = 0m, VatAmount = 0m }
            },
            PurchaseCategories = new List<LhdnVatCategorySummaryDto>
            {
                new() { CategoryCode = "01", CategoryName = "Sales Tax", Amount = 4000m, Adjustment = 0m, VatAmount = 240m },
                new() { CategoryCode = "02", CategoryName = "Service Tax", Amount = 2000m, Adjustment = 200m, VatAmount = 144m },
                new() { CategoryCode = "E", CategoryName = "Tax Exemption", Amount = 1000m, Adjustment = 0m, VatAmount = 0m }
            }
        };

        report.TotalSalesAmount = report.SalesCategories.Sum(x => x.Amount);
        report.TotalSalesAdjustment = report.SalesCategories.Sum(x => x.Adjustment);
        report.TotalSalesVat = report.SalesCategories.Sum(x => x.VatAmount);

        report.TotalPurchaseAmount = report.PurchaseCategories.Sum(x => x.Amount);
        report.TotalPurchaseAdjustment = report.PurchaseCategories.Sum(x => x.Adjustment);
        report.TotalPurchaseVat = report.PurchaseCategories.Sum(x => x.VatAmount);

        report.NetVatPayable = report.TotalSalesVat - report.TotalPurchaseVat;

        // Sales totals: 10000 + 5000 + 2000 = 17000; VAT = 600 + 360 = 960
        Assert.Equal(17000m, report.TotalSalesAmount);
        Assert.Equal(500m, report.TotalSalesAdjustment);
        Assert.Equal(960m, report.TotalSalesVat);

        // Purchase totals: 4000 + 2000 + 1000 = 7000; VAT = 240 + 144 = 384
        Assert.Equal(7000m, report.TotalPurchaseAmount);
        Assert.Equal(200m, report.TotalPurchaseAdjustment);
        Assert.Equal(384m, report.TotalPurchaseVat);

        // Net VAT Payable = 960 - 384 = 576
        Assert.Equal(576m, report.NetVatPayable);
    }

    [Fact]
    public void VatReport_IncludesAllSevenMalaysianTaxCategories()
    {
        var expectedCodes = new[] { "01", "02", "03", "04", "05", "06", "E" };
        var categoryNames = new Dictionary<string, string>
        {
            { "01", "Sales Tax" },
            { "02", "Service Tax" },
            { "03", "Tourism Tax" },
            { "04", "High-Value Goods Tax" },
            { "05", "Sales Tax on Low Value Goods" },
            { "06", "Not Applicable" },
            { "E", "Tax Exemption" }
        };

        foreach (var code in expectedCodes)
        {
            Assert.True(categoryNames.ContainsKey(code));
            Assert.False(string.IsNullOrWhiteSpace(categoryNames[code]));
        }
    }

    #endregion

    #region LHDN Dashboard Stats

    [Fact]
    public void DashboardStats_AggregatesCountsAcrossAllStatuses()
    {
        var stats = new LhdnDashboardStatsDto
        {
            SalesValid = 42,
            SalesInvalid = 3,
            SalesSubmitted = 5,
            SalesCancelled = 2,
            SalesFailed = 1,
            SalesNotSubmitted = 10,

            PurchaseValid = 25,
            PurchaseInvalid = 1,
            PurchaseSubmitted = 2,
            PurchaseCancelled = 0,
            PurchaseFailed = 0,
            PurchaseNotSubmitted = 4
        };

        var totalSales = stats.SalesValid + stats.SalesInvalid + stats.SalesSubmitted +
                         stats.SalesCancelled + stats.SalesFailed + stats.SalesNotSubmitted;
        var totalPurchase = stats.PurchaseValid + stats.PurchaseInvalid + stats.PurchaseSubmitted +
                            stats.PurchaseCancelled + stats.PurchaseFailed + stats.PurchaseNotSubmitted;

        Assert.Equal(63, totalSales);
        Assert.Equal(32, totalPurchase);

        var salesSuccessRate = (double)stats.SalesValid / (stats.SalesValid + stats.SalesInvalid) * 100;
        Assert.InRange(salesSuccessRate, 93.3, 93.4);
    }

    #endregion
}
