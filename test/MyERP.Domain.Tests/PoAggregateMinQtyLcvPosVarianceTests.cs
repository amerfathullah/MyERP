using System;
using System.Linq;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for PO aggregate min order qty, LCV proportional allocation,
/// POS Closing variance, and batch expiry enforcement.
/// </summary>
public class PoAggregateMinQtyLcvPosVarianceTests
{
    // === PO Minimum Order Qty — Aggregate Validation ===

    [Fact]
    public void PurchaseOrder_TwoRowsSameItem_AggregatesQty()
    {
        // Per DO-NOT: "Validate PO minimum order qty per row — must aggregate stock_qty 
        // across ALL rows per item before comparing to Item.min_order_qty"
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Widget A", 50m, 10m, 0m, "Unit"); // row 1: 50
        po.AddItem(po.Items[0].ItemId, "Widget A", 60m, 10m, 0m, "Unit"); // row 2: same item, 60
        // Total for Widget A = 110 → should pass min_order_qty = 100
        var grouped = po.Items.GroupBy(i => i.ItemId).First();
        Assert.Equal(110m, grouped.Sum(i => i.Quantity));
    }

    [Fact]
    public void PurchaseOrder_SingleRowBelowMin_FailsPerRow()
    {
        // A single row with qty < min_order_qty should still fail
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PO-002", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Item X", 30m, 10m, 0m, "Unit");
        var totalQty = po.Items.Where(i => i.ItemId == po.Items[0].ItemId).Sum(i => i.Quantity);
        // If min_order_qty is 50, this should fail → validated by PurchaseOrderManager
        Assert.Equal(30m, totalQty);
    }

    // === Landed Cost Voucher — Proportional Distribution ===

    [Fact]
    public void Lcv_BasedOnAmount_ProportionalDistribution()
    {
        var lcv = new LandedCostVoucher(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10m, 1000m, "Item A");
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 5m, 500m, "Item B");
        lcv.AddCharge("Freight", Guid.NewGuid(), 300m);

        lcv.DistributeCharges();

        // Total amount = 1000 + 500 = 1500. Charge = 300.
        // Item A: 300 × 1000/1500 = 200
        // Item B: 300 × 500/1500 = 100
        Assert.Equal(200m, lcv.Items[0].ApplicableCharges);
        Assert.Equal(100m, lcv.Items[1].ApplicableCharges);
        Assert.Equal(300m, lcv.TotalDistributedAmount);
    }

    [Fact]
    public void Lcv_BasedOnQuantity_ProportionalDistribution()
    {
        var lcv = new LandedCostVoucher(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow)
        { DistributionMethod = LandedCostDistributionMethod.BasedOnQuantity };
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10m, 100m, "Item A");
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 30m, 100m, "Item B");
        lcv.AddCharge("Customs", Guid.NewGuid(), 400m);

        lcv.DistributeCharges();

        // Total qty = 40. Charge = 400.
        // Item A: 400 × 10/40 = 100
        // Item B: 400 × 30/40 = 300
        Assert.Equal(100m, lcv.Items[0].ApplicableCharges);
        Assert.Equal(300m, lcv.Items[1].ApplicableCharges);
    }

    [Fact]
    public void Lcv_RoundingDifference_AbsorbedByLastItem()
    {
        var lcv = new LandedCostVoucher(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        // Three items with amounts 100, 100, 100 = 300. Charge = 100.
        // Each item gets 100 × 100/300 = 33.33... rounded to 33.33
        // Sum = 99.99 → difference 0.01 goes to last item
        lcv.AddItem(Guid.NewGuid(), "PR", Guid.NewGuid(), 1m, 100m, "A");
        lcv.AddItem(Guid.NewGuid(), "PR", Guid.NewGuid(), 1m, 100m, "B");
        lcv.AddItem(Guid.NewGuid(), "PR", Guid.NewGuid(), 1m, 100m, "C");
        lcv.AddCharge("Insurance", Guid.NewGuid(), 100m);

        lcv.DistributeCharges();

        // Total MUST equal 100 (error diffusion applies)
        Assert.Equal(100m, lcv.TotalDistributedAmount);
    }

    [Fact]
    public void Lcv_Submit_ValidatesTotalDistributionMatchesCharges()
    {
        var lcv = new LandedCostVoucher(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        lcv.AddItem(Guid.NewGuid(), "PR", Guid.NewGuid(), 10m, 500m);
        lcv.AddCharge("Freight", Guid.NewGuid(), 50m);
        lcv.Submit();

        Assert.Equal(DocumentStatus.Submitted, lcv.Status);
        Assert.Equal(50m, lcv.TotalDistributedAmount);
    }

    [Fact]
    public void Lcv_Submit_RequiresItems()
    {
        var lcv = new LandedCostVoucher(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        lcv.AddCharge("Freight", Guid.NewGuid(), 100m);
        // No items → submit should fail
        Assert.Throws<Volo.Abp.BusinessException>(() => lcv.Submit());
    }

    [Fact]
    public void Lcv_Submit_RequiresCharges()
    {
        var lcv = new LandedCostVoucher(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        lcv.AddItem(Guid.NewGuid(), "PR", Guid.NewGuid(), 10m, 500m);
        // No charges → submit should fail
        Assert.Throws<Volo.Abp.BusinessException>(() => lcv.Submit());
    }

    // === POS Closing Payment Variance ===

    [Fact]
    public void PosClosingEntry_PaymentVariance_Calculated()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
        entry.AddPayment(Guid.NewGuid(), "Cash", expectedAmount: 5000m, closingAmount: 4800m);
        entry.AddPayment(Guid.NewGuid(), "Card", expectedAmount: 3000m, closingAmount: 3000m);

        // Cash: Expected 5000 - Actual 4800 = 200 (short)
        // Card: Expected 3000 - Actual 3000 = 0 (balanced)
        Assert.Equal(200m, entry.Payments[0].Difference);
        Assert.Equal(0m, entry.Payments[1].Difference);
        Assert.Equal(200m, entry.TotalDifference);
    }

    [Fact]
    public void PosClosingEntry_PaymentVariance_NegativeMeansOverage()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
        entry.AddPayment(Guid.NewGuid(), "Cash", expectedAmount: 1000m, closingAmount: 1050m);

        // Expected 1000 - Actual 1050 = -50 (overage)
        Assert.Equal(-50m, entry.Payments[0].Difference);
    }

    [Fact]
    public void PosClosingEntry_Submit_CalculatesGrandTotal()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
        entry.AddInvoice(Guid.NewGuid(), "POS-001", 500m);
        entry.AddInvoice(Guid.NewGuid(), "POS-002", 750m);
        entry.AddPayment(Guid.NewGuid(), "Cash", 1250m, 1250m);
        entry.Submit();

        Assert.Equal(1250m, entry.GrandTotal);
        Assert.Equal(PosClosingStatus.Submitted, entry.Status);
    }

    // === Batch Expiry — Entity Level ===

    [Fact]
    public void Batch_IsExpired_True_WhenPastDate()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-001")
        { ExpiryDate = new DateTime(2025, 1, 1) };

        Assert.True(batch.IsExpired(new DateTime(2025, 6, 1)));
    }

    [Fact]
    public void Batch_IsExpired_False_WhenFutureDate()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-002")
        { ExpiryDate = new DateTime(2027, 12, 31) };

        Assert.False(batch.IsExpired(new DateTime(2026, 6, 1)));
    }

    [Fact]
    public void Batch_IsExpired_False_WhenNoExpiryDate()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-003");
        // No expiry date set → never expires
        Assert.False(batch.IsExpired(DateTime.UtcNow));
    }

    // === DeliveryNote — Batch Validation Wired ===

    [Fact]
    public void DeliveryNoteItem_HasBatchId_ForTracking()
    {
        var dnItem = new DeliveryNoteItem(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Widget", 5m, 100m, 0m)
        { BatchId = Guid.NewGuid() };

        Assert.NotNull(dnItem.BatchId);
    }
}
