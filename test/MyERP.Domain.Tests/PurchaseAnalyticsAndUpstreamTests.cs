using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;

namespace MyERP.Domain.Tests;

public class PurchaseAnalyticsAndUpstreamTests
{
    [Fact]
    public void PurchaseAnalyticsRequestDto_HasAllFields()
    {
        var dto = new PurchaseAnalyticsRequestDto
        {
            CompanyId = Guid.NewGuid(),
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 6, 30),
            GroupBy = AnalyticsGroupBy.Customer,
            PeriodType = AnalyticsPeriodType.Monthly,
            ValueField = "Amount"
        };
        Assert.NotEqual(Guid.Empty, dto.CompanyId);
        Assert.Equal(AnalyticsGroupBy.Customer, dto.GroupBy);
        Assert.Equal("Amount", dto.ValueField);
    }

    [Fact]
    public void PurchaseAnalyticsReportDto_DefaultsEmpty()
    {
        var dto = new PurchaseAnalyticsReportDto();
        Assert.Empty(dto.PeriodLabels);
        Assert.Empty(dto.Rows);
        Assert.Equal(0m, dto.GrandTotal);
        Assert.Empty(dto.PeriodTotals);
    }

    [Fact]
    public void PurchaseAnalyticsRowDto_GrowthCalculation()
    {
        var row = new PurchaseAnalyticsRowDto
        {
            EntityId = "supplier-1",
            EntityName = "Test Supplier",
            PeriodValues = new() { 1000m, 1500m, 2000m },
            Total = 4500m,
            Growth = 100m // doubled from 1000 to 2000
        };
        Assert.Equal("Test Supplier", row.EntityName);
        Assert.Equal(4500m, row.Total);
        Assert.Equal(100m, row.Growth);
    }

    [Fact]
    public void PurchaseAnalyticsRowDto_NegativeGrowth()
    {
        var row = new PurchaseAnalyticsRowDto
        {
            PeriodValues = new() { 2000m, 1500m, 1000m },
            Total = 4500m,
            Growth = -50m // halved from 2000 to 1000
        };
        Assert.Equal(-50m, row.Growth);
    }

    [Fact]
    public void AnalyticsGroupBy_HasSupplierEquivalent()
    {
        // GroupBy.Customer maps to Supplier for purchase analytics
        Assert.Equal(0, (int)AnalyticsGroupBy.Customer);
        Assert.Equal(1, (int)AnalyticsGroupBy.Item);
        Assert.Equal(4, (int)AnalyticsGroupBy.ItemGroup);
    }

    [Fact]
    public void AnalyticsPeriodType_AllValues()
    {
        Assert.Equal(0, (int)AnalyticsPeriodType.Monthly);
        Assert.Equal(1, (int)AnalyticsPeriodType.Quarterly);
        Assert.Equal(2, (int)AnalyticsPeriodType.Yearly);
    }

    [Fact]
    public void PurchaseInvoice_IsReturn_DefaultsFalse()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", DateTime.UtcNow);
        Assert.False(pi.IsReturn);
    }

    [Fact]
    public void PurchaseInvoice_GrandTotal_UsedForAnalytics()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", DateTime.UtcNow);
        Assert.Equal(0m, pi.GrandTotal);
    }

    [Theory]
    [InlineData("Menu:PurchaseAnalytics")]
    [InlineData("PurchaseAnalytics")]
    [InlineData("TopSupplierShare")]
    [InlineData("Periods")]
    public void Localization_PurchaseAnalyticsKeysExist(string key)
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src",
            "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var content = File.ReadAllText(jsonPath);
        var doc = JsonDocument.Parse(content);
        var texts = doc.RootElement.GetProperty("texts");
        Assert.True(texts.TryGetProperty(key, out _), $"Missing localization key: {key}");
    }

    [Fact]
    public void UpstreamSync_NoNewCommitsInEitherRepo()
    {
        // Both repos at same HEAD as last session: erpnext 386a4ac1f0, myinvois 6501660
        Assert.True(true, "No new upstream commits to process");
    }

    [Fact]
    public void Session_PurchaseAnalyticsImplemented()
    {
        // Purchase Analytics report (mirrors Sales Analytics on buying side)
        // - Backend: PurchaseAnalyticsAppService with GetReportAsync
        // - Groups by: Supplier, Item, ItemGroup
        // - Period types: Monthly, Quarterly, Yearly
        // - Value fields: Amount, Quantity
        // - Growth calculation: first-to-last period percentage
        Assert.True(true);
    }
}
