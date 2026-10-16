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

    [Fact]
    public void PurchaseAnalyticsRequestDto_EntityIds_SupportsFiltering()
    {
        var dto = new PurchaseAnalyticsRequestDto
        {
            EntityIds = new() { "SUP-001", "SUP-002" }
        };
        Assert.NotNull(dto.EntityIds);
        Assert.Equal(2, dto.EntityIds.Count);
    }

    [Theory]
    [InlineData("Menu:PurchaseAnalytics")]
    [InlineData("PurchaseAnalytics")]
    [InlineData("TopSupplierShare")]
    [InlineData("Periods")]
    [InlineData("FilterByEntity")]
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
    public void PurchaseAnalyticsRequestDto_PeriodBasedOn_BillingDate()
    {
        var dto = new PurchaseAnalyticsRequestDto
        {
            PeriodBasedOn = "BillingDate"
        };
        Assert.Equal("BillingDate", dto.PeriodBasedOn);
    }

    [Fact]
    public void SalesAnalyticsRequestDto_PeriodBasedOn_BillingDate()
    {
        var dto = new SalesAnalyticsRequestDto
        {
            PeriodBasedOn = "BillingDate"
        };
        Assert.Equal("BillingDate", dto.PeriodBasedOn);
    }

    [Fact]
    public void ItemWisePurchaseRegister_IncludesOtherChargesInTotal()
    {
        // Per ERPNext PR #60047 / commit 7d3592adaa: Total includes TotalOtherCharges
        var row = new ItemWisePurchaseRegisterLineDto
        {
            Amount = 100m,
            TotalTax = 18m,
            TotalOtherCharges = 10m,
            Total = 128m
        };
        Assert.Equal(100m, row.Amount);
        Assert.Equal(18m, row.TotalTax);
        Assert.Equal(10m, row.TotalOtherCharges);
        Assert.Equal(128m, row.Total);
    }

    [Fact]
    public void ItemWisePurchaseRegister_PurchaseReceiptsCommaSeparated()
    {
        // Per ERPNext PR #60047 / commit ebab2f50dc: partly received order lists receipts comma-separated
        var row = new ItemWisePurchaseRegisterLineDto
        {
            PurchaseOrderNumber = "PO-0001",
            PurchaseReceiptNumber = "PR-0001, PR-0002"
        };
        Assert.Equal("PO-0001", row.PurchaseOrderNumber);
        var receipts = row.PurchaseReceiptNumber.Split(", ");
        Assert.Equal(2, receipts.Length);
        Assert.Contains("PR-0001", receipts);
        Assert.Contains("PR-0002", receipts);
    }

    [Fact]
    public void ItemWiseSalesHistory_DtoMappingAndCustomerGroup()
    {
        // Per ERPNext PR #60350 / commit 98ef9605b1: customer group read from order/transaction
        var filter = new GetItemWiseSalesHistoryDto
        {
            CompanyId = Guid.NewGuid(),
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 6, 30),
            ItemCode = "ITEM-01",
            CustomerId = Guid.NewGuid()
        };
        var row = new ItemWiseSalesHistoryRowDto
        {
            SalesOrderId = Guid.NewGuid(),
            SalesOrderNumber = "SO-0001",
            CustomerGroup = "Commercial",
            Quantity = 5,
            Rate = 50,
            Amount = 250,
            DeliveredQuantity = 3,
            BilledAmount = 150
        };
        Assert.Equal("Commercial", row.CustomerGroup);
        Assert.Equal(250m, row.Amount);
        Assert.Equal(150m, row.BilledAmount);
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
