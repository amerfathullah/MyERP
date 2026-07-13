using System;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Sales;

public class GrossProfitTests
{
    private static SalesInvoice CreateSIWithItems()
    {
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Widget A", 10m, 150m, 0m);
        si.AddItem(Guid.NewGuid(), "Widget B", 5m, 200m, 0m);
        return si;
    }

    [Fact]
    public void SalesInvoiceItem_ValuationRate_DefaultZero()
    {
        var si = CreateSIWithItems();
        si.Items[0].ValuationRate.ShouldBe(0m);
    }

    [Fact]
    public void SalesInvoiceItem_GrossProfit_Calculated()
    {
        var si = CreateSIWithItems();
        si.Items[0].ValuationRate = 80m; // Cost: RM80/unit
        // GrossProfit = (150 - 80) × 10 = 700
        si.Items[0].GrossProfit.ShouldBe(700m);
    }

    [Fact]
    public void SalesInvoiceItem_GrossProfit_ZeroCost()
    {
        var si = CreateSIWithItems();
        si.Items[0].ValuationRate = 0m;
        // GrossProfit = (150 - 0) × 10 = 1500
        si.Items[0].GrossProfit.ShouldBe(1500m);
    }

    [Fact]
    public void GrossProfitService_CalculateForInvoice()
    {
        var si = CreateSIWithItems();
        si.Items[0].ValuationRate = 80m;  // Selling 150, Cost 80
        si.Items[1].ValuationRate = 120m; // Selling 200, Cost 120

        var service = new GrossProfitService(null!);
        var result = service.CalculateForInvoice(si);

        // Revenue: (10×150) + (5×200) = 1500 + 1000 = 2500
        result.TotalRevenue.ShouldBe(2500m);
        // Cost: (10×80) + (5×120) = 800 + 600 = 1400
        result.TotalCost.ShouldBe(1400m);
        // Gross Profit: 2500 - 1400 = 1100
        result.GrossProfit.ShouldBe(1100m);
        // GP%: 1100/2500 × 100 = 44%
        result.GrossProfitPercentage.ShouldBe(44m);
    }

    [Fact]
    public void GrossProfitService_PerItemDetail()
    {
        var si = CreateSIWithItems();
        si.Items[0].ValuationRate = 100m; // Selling 150, Cost 100 → 33.33% margin
        si.Items[1].ValuationRate = 150m; // Selling 200, Cost 150 → 25% margin

        var service = new GrossProfitService(null!);
        var result = service.CalculateForInvoice(si);

        result.ItemDetails.Count.ShouldBe(2);
        result.ItemDetails[0].GrossProfitPercentage.ShouldBe(33.33m);
        result.ItemDetails[1].GrossProfitPercentage.ShouldBe(25m);
    }

    [Fact]
    public void DeliveryNoteItem_ValuationRate_DefaultZero()
    {
        var dnItem = new DeliveryNoteItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 10, 100, 0);
        dnItem.ValuationRate.ShouldBe(0m);
    }

    [Fact]
    public void DeliveryNoteItem_ValuationRate_CanBeSet()
    {
        var dnItem = new DeliveryNoteItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 10, 100, 0);
        dnItem.ValuationRate = 75m;
        dnItem.ValuationRate.ShouldBe(75m);
    }

    [Fact]
    public void GrossProfit_NegativeMargin_Detected()
    {
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Loss Item", 10m, 50m, 0m);
        si.Items[0].ValuationRate = 80m; // Selling below cost!

        // GrossProfit = (50 - 80) × 10 = -300
        si.Items[0].GrossProfit.ShouldBe(-300m);

        var service = new GrossProfitService(null!);
        var result = service.CalculateForInvoice(si);
        result.GrossProfit.ShouldBeLessThan(0);
    }
}
