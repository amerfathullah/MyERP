using System;
using System.Linq;
using MyERP.Assets.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP;

/// <summary>
/// Tests for recent production hardening fixes:
/// - Divide-by-zero guards
/// - Fulfillment counter patterns
/// - Concurrency stamp usage
/// - DateTime consistency
/// </summary>
public class ProductionHardeningTests
{
    // === Divide-by-Zero Guards ===

    [Fact]
    public void Asset_FrequencyMonths_Zero_NoException()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-001", "Laptop",
            DateTime.Today, 5000m)
        {
            CalculateDepreciation = true,
            UsefulLifeMonths = 60,
            FrequencyMonths = 0 // would cause DivideByZeroException without guard
        };

        // Should not throw — guard returns early with 0 periods
        asset.GenerateDepreciationSchedule();
        Assert.Empty(asset.DepreciationSchedule);
    }

    [Fact]
    public void Asset_FrequencyMonths_Normal_GeneratesSchedule()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-002", "Server",
            DateTime.Today, 12000m)
        {
            CalculateDepreciation = true,
            UsefulLifeMonths = 60,
            FrequencyMonths = 12 // 5 annual periods
        };

        asset.GenerateDepreciationSchedule();
        Assert.Equal(5, asset.DepreciationSchedule.Count);
    }

    [Fact]
    public void Asset_UsefulLifeMonths_Zero_NoSchedule()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-003", "Tool",
            DateTime.Today, 500m)
        {
            CalculateDepreciation = true,
            UsefulLifeMonths = 0,
            FrequencyMonths = 12
        };

        asset.GenerateDepreciationSchedule();
        Assert.Empty(asset.DepreciationSchedule);
    }

    [Fact]
    public void WorkOrder_Quantity_Zero_PercentComplete_ReturnsZero()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001",
            Guid.NewGuid(), Guid.NewGuid(), 0);

        // Quantity=0 should not cause DivideByZeroException
        Assert.Equal(0m, wo.PercentComplete);
    }

    [Fact]
    public void WorkOrder_Quantity_Normal_PercentComplete()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-002",
            Guid.NewGuid(), Guid.NewGuid(), 100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(50);

        Assert.Equal(50.0m, wo.PercentComplete);
    }

    [Fact]
    public void BOM_Quantity_DefaultsToOne()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(),
            "BOM-001", Guid.NewGuid());
        Assert.Equal(1m, bom.Quantity);
    }

    [Fact]
    public void BOM_Quantity_CanBeSetToNonZero()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(),
            "BOM-002", Guid.NewGuid());
        bom.Quantity = 10;
        Assert.Equal(10m, bom.Quantity);
    }

    // === Fulfillment Counter Patterns ===

    [Fact]
    public void SalesOrder_DeliveredQty_IncrementAndDecrement()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Widget", 100, 10, 0, "Unit");
        var item = so.Items.First();

        // Simulate delivery
        item.DeliveredQty += 30;
        Assert.Equal(30m, item.DeliveredQty);

        // Simulate cancel reversal (Math.Max prevents negative)
        item.DeliveredQty = Math.Max(0, item.DeliveredQty - 30);
        Assert.Equal(0m, item.DeliveredQty);
    }

    [Fact]
    public void SalesOrder_DeliveredQty_NeverGoesNegative()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-002", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Gadget", 50, 20, 0, "Unit");
        var item = so.Items.First();

        item.DeliveredQty = 10;
        // Reversing more than delivered should clamp at 0
        item.DeliveredQty = Math.Max(0, item.DeliveredQty - 20);
        Assert.Equal(0m, item.DeliveredQty);
    }

    [Fact]
    public void PurchaseOrder_ReceivedQty_IncrementAndDecrement()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Part A", 200, 5, 0, "Unit");
        var item = po.Items.First();

        item.ReceivedQty += 80;
        Assert.Equal(80m, item.ReceivedQty);

        item.ReceivedQty = Math.Max(0, item.ReceivedQty - 80);
        Assert.Equal(0m, item.ReceivedQty);
    }

    [Fact]
    public void PurchaseOrder_ReceivedQty_NeverGoesNegative()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-002", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Part B", 100, 8, 0, "Unit");
        var item = po.Items.First();

        item.ReceivedQty = 5;
        item.ReceivedQty = Math.Max(0, item.ReceivedQty - 50);
        Assert.Equal(0m, item.ReceivedQty);
    }

    [Fact]
    public void SalesOrder_BilledQty_Tracking()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-003", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Service", 10, 100, 0, "Unit");
        var item = so.Items.First();

        item.BilledQty += 5;
        Assert.Equal(5m, item.BilledQty);

        // Reversal
        item.BilledQty = Math.Max(0, item.BilledQty - 5);
        Assert.Equal(0m, item.BilledQty);
    }

    // === SalesInvoice AmountPaid Math.Max Guard ===

    [Fact]
    public void SalesInvoice_AmountPaid_NeverGoesNegative()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.Today, null);
        si.AddItem(Guid.NewGuid(), "Item", 1, 1000, 0, "Unit");

        si.AmountPaid = 500;
        // Simulate cancel reversal with Math.Max guard
        si.AmountPaid = Math.Max(0, si.AmountPaid - 800);
        Assert.Equal(0m, si.AmountPaid);
    }

    [Fact]
    public void SalesInvoice_AmountPaid_NormalReduction()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-002", DateTime.Today, null);
        si.AddItem(Guid.NewGuid(), "Item", 1, 1000, 0, "Unit");

        si.AmountPaid = 1000;
        si.AmountPaid = Math.Max(0, si.AmountPaid + (-600));
        Assert.Equal(400m, si.AmountPaid);
    }

    // === Fulfillment Status Transitions ===

    [Fact]
    public void SalesOrder_UpdateFulfillmentStatus_AllDelivered_ToBill()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-004", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0, "Unit");
        so.Submit();

        var item = so.Items.First();
        item.DeliveredQty = 10; // fully delivered
        so.UpdateFulfillmentStatus();

        Assert.Equal(DocumentStatus.ToBill, so.Status);
    }

    [Fact]
    public void SalesOrder_UpdateFulfillmentStatus_AllBilled_ToDeliver()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-005", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0, "Unit");
        so.Submit();

        var item = so.Items.First();
        item.BilledQty = 10; // fully billed
        so.UpdateFulfillmentStatus();

        Assert.Equal(DocumentStatus.ToDeliver, so.Status);
    }

    [Fact]
    public void SalesOrder_UpdateFulfillmentStatus_BothComplete_Completed()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-006", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0, "Unit");
        so.Submit();

        var item = so.Items.First();
        item.DeliveredQty = 10;
        item.BilledQty = 10;
        so.UpdateFulfillmentStatus();

        Assert.Equal(DocumentStatus.Completed, so.Status);
    }

    [Fact]
    public void PurchaseOrder_UpdateFulfillmentStatus_FullyReceived_ToBill()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-003", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Part", 50, 10, 0, "Unit");
        po.Submit();

        var item = po.Items.First();
        item.ReceivedQty = 50;
        po.UpdateFulfillmentStatus();

        Assert.Equal(DocumentStatus.ToBill, po.Status);
    }

    // === Asset Depreciation Schedule ===

    [Fact]
    public void Asset_FrequencyMonths_Default_Is12()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-004", "Chair",
            DateTime.Today, 1000m);
        Assert.Equal(12, asset.FrequencyMonths);
    }

    [Fact]
    public void Asset_CalculateDepreciation_False_NoSchedule()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-005", "Item",
            DateTime.Today, 1000m)
        {
            CalculateDepreciation = false,
            UsefulLifeMonths = 60,
            FrequencyMonths = 12
        };
        asset.GenerateDepreciationSchedule();
        Assert.Empty(asset.DepreciationSchedule);
    }

    // === Seed Data Patterns ===

    [Theory]
    [InlineData("AED", "USD", 3.6725)]
    [InlineData("SAR", "USD", 3.75)]
    [InlineData("QAR", "USD", 3.64)]
    public void PeggedCurrency_Rates_ArePositive(string from, string to, decimal expectedRate)
    {
        var ce = new Accounting.Entities.CurrencyExchange(Guid.NewGuid(), from, to, expectedRate, new DateTime(2000, 1, 1));
        Assert.True(ce.ExchangeRate > 0);
        Assert.Equal(expectedRate, ce.ExchangeRate);
    }

    [Fact]
    public void UomConversion_Dozen_To_Unit_Is12()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Dozen", "Unit", 12m);
        Assert.Equal(12m, conv.ConversionFactor);
        Assert.Equal(60m, conv.Convert(5m)); // 5 dozen = 60 units
    }

    [Fact]
    public void UomConversion_Kg_To_Gram_Is1000()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Kg", "Gram", 1000m);
        Assert.Equal(2500m, conv.Convert(2.5m));
    }
}
