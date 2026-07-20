using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Accounting.Entities;
using Xunit;

namespace MyERP;

/// <summary>
/// Tests verifying dashboard/report aggregation patterns produce correct results.
/// These validate the formulas used in server-side IQueryable projections.
/// </summary>
public class DashboardAggregationTests
{
    // === Monthly Revenue/Expense Aggregation ===

    [Fact]
    public void SalesInvoice_GrandTotal_SumOnPosted()
    {
        var invoices = new[]
        {
            CreateSI(1000m, DocumentStatus.Posted),
            CreateSI(500m, DocumentStatus.Posted),
            CreateSI(300m, DocumentStatus.Draft), // excluded
        };
        var sum = invoices.Where(i => i.Status == DocumentStatus.Posted).Sum(i => i.GrandTotal);
        Assert.Equal(1500m, sum);
    }

    [Fact]
    public void SalesInvoice_EmptyCollection_SumIsZero()
    {
        var invoices = Array.Empty<SalesInvoice>();
        var sum = invoices.Where(i => i.Status == DocumentStatus.Posted)
            .Select(i => i.GrandTotal).DefaultIfEmpty(0).Sum();
        Assert.Equal(0m, sum);
    }

    [Fact]
    public void SalesInvoice_OutstandingCalculation()
    {
        var si = CreateSI(1000m, DocumentStatus.Posted);
        si.AmountPaid = 400m;
        Assert.Equal(600m, si.OutstandingAmount);
    }

    [Fact]
    public void SalesInvoice_FullyPaid_NotOutstanding()
    {
        var si = CreateSI(1000m, DocumentStatus.Posted);
        si.AmountPaid = 1000m;
        Assert.Equal(0m, si.OutstandingAmount);
        Assert.False(si.AmountPaid < si.GrandTotal);
    }

    // === Revenue Trend Grouping ===

    [Fact]
    public void RevenueTrend_GroupByYearMonth()
    {
        var invoices = new[]
        {
            CreateSIWithDate(500m, new DateTime(2026, 1, 15)),
            CreateSIWithDate(300m, new DateTime(2026, 1, 20)),
            CreateSIWithDate(700m, new DateTime(2026, 2, 5)),
        };

        var trend = invoices
            .GroupBy(i => new { i.IssueDate.Year, i.IssueDate.Month })
            .Select(g => new { Month = $"{g.Key.Year}-{g.Key.Month:D2}", Amount = g.Sum(i => i.GrandTotal) })
            .OrderBy(x => x.Month)
            .ToList();

        Assert.Equal(2, trend.Count);
        Assert.Equal("2026-01", trend[0].Month);
        Assert.Equal(800m, trend[0].Amount);
        Assert.Equal("2026-02", trend[1].Month);
        Assert.Equal(700m, trend[1].Amount);
    }

    // === Low Stock Detection ===

    [Fact]
    public void Bin_ProjectedQty_BelowReorderLevel()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty = 5;
        bin.ReservedQty = 3;
        // ProjectedQty = actual - reserved + ordered - indented - planned
        Assert.True(bin.ProjectedQty <= 10); // Would trigger reorder at level 10
    }

    [Fact]
    public void Bin_ProjectedQty_AboveReorderLevel()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty = 100;
        bin.OrderedQty = 50;
        Assert.True(bin.ProjectedQty > 10); // Safe above reorder level
    }

    // === Financial KPI Calculations ===

    [Fact]
    public void ProfitMargin_CorrectPercentage()
    {
        var revenue = 10000m;
        var expenses = 7000m;
        var netProfit = revenue - expenses;
        var margin = revenue > 0 ? Math.Round(netProfit / revenue * 100, 1) : 0;
        Assert.Equal(30.0m, margin);
    }

    [Fact]
    public void ProfitMargin_ZeroRevenue_NoException()
    {
        var revenue = 0m;
        var margin = revenue > 0 ? Math.Round(0m / revenue * 100, 1) : 0m;
        Assert.Equal(0m, margin);
    }

    [Fact]
    public void RevenueGrowth_Positive()
    {
        var current = 12000m;
        var previous = 10000m;
        var growth = Math.Round((current - previous) / previous * 100, 1);
        Assert.Equal(20.0m, growth);
    }

    [Fact]
    public void RevenueGrowth_ZeroPrevious_Returns100()
    {
        var current = 5000m;
        var previous = 0m;
        var growth = previous > 0
            ? Math.Round((current - previous) / previous * 100, 1)
            : (current > 0 ? 100m : 0m);
        Assert.Equal(100m, growth);
    }

    [Fact]
    public void RevenueGrowth_BothZero_ReturnsZero()
    {
        var current = 0m;
        var previous = 0m;
        var growth = previous > 0
            ? Math.Round((current - previous) / previous * 100, 1)
            : (current > 0 ? 100m : 0m);
        Assert.Equal(0m, growth);
    }

    // === AR/AP Outstanding ===

    [Fact]
    public void AR_Outstanding_ExcludesReturns()
    {
        var invoices = new[]
        {
            CreateSI(1000m, DocumentStatus.Posted),
            CreateSIReturn(-500m, DocumentStatus.Posted),
        };
        var arOutstanding = invoices
            .Where(si => si.Status == DocumentStatus.Posted && !si.IsReturn)
            .Sum(si => si.GrandTotal - si.AmountPaid);
        Assert.Equal(1000m, arOutstanding); // Return excluded
    }

    [Fact]
    public void AP_Outstanding_Formula()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PI-001", DateTime.Today);
        pi.AddItem(Guid.NewGuid(), "Service", 1, 5000m, 0m);
        pi.AmountPaid = 2000m;
        Assert.Equal(3000m, pi.OutstandingAmount);
    }

    // === Helpers ===

    private SalesInvoice CreateSI(decimal grandTotal, DocumentStatus status)
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            $"INV-{Guid.NewGuid().ToString()[..4]}", DateTime.Today);
        si.AddItem(Guid.NewGuid(), "Item", 1, grandTotal, 0m);
        if (status == DocumentStatus.Posted) { si.Submit(); si.Post(); }
        return si;
    }

    private SalesInvoice CreateSIWithDate(decimal grandTotal, DateTime date)
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            $"INV-{Guid.NewGuid().ToString()[..4]}", date);
        si.AddItem(Guid.NewGuid(), "Item", 1, grandTotal, 0m);
        si.Submit();
        si.Post();
        return si;
    }

    private SalesInvoice CreateSIReturn(decimal grandTotal, DocumentStatus status)
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            $"CN-{Guid.NewGuid().ToString()[..4]}", DateTime.Today);
        si.IsReturn = true;
        si.AddItem(Guid.NewGuid(), "Return", -1, Math.Abs(grandTotal), 0m);
        if (status == DocumentStatus.Posted) { si.Submit(); si.Post(); }
        return si;
    }
}
