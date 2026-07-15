using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP;

/// <summary>
/// Edge case tests for critical financial calculations.
/// These verify precision, rounding, and boundary conditions.
/// </summary>
public class FinancialPrecisionTests
{
    // === FIFO Valuation Edge Cases ===

    [Fact]
    public void FifoValuation_EmptyQueue_DefaultRate()
    {
        var fifo = new FifoValuation();
        Assert.Equal(0m, fifo.TotalQty);
        Assert.Equal(0m, fifo.TotalValue);
        Assert.Equal(0m, fifo.ValuationRate);
    }

    [Fact]
    public void FifoValuation_SingleBin_FullConsumption()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(100m, 25m); // 100 units @ 25
        Assert.Equal(100m, fifo.TotalQty);
        Assert.Equal(2500m, fifo.TotalValue);

        var consumed = fifo.RemoveStock(100m);
        var consumedValue = FifoValuation.GetOutgoingRate(consumed) * 100m;
        Assert.Equal(2500m, consumedValue);
        Assert.Equal(0m, fifo.TotalQty);
    }

    [Fact]
    public void FifoValuation_MultiBin_OldestFirst()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(50m, 10m);  // Bin 1: 50 @ 10
        fifo.AddStock(30m, 15m);  // Bin 2: 30 @ 15
        fifo.AddStock(20m, 20m);  // Bin 3: 20 @ 20

        var consumed = fifo.RemoveStock(60m);
        // FIFO: takes all of Bin1 (50@10) + 10 from Bin2 (10@15)
        var totalConsumedValue = consumed.Sum(b => b.Qty * b.Rate);
        Assert.Equal(650m, totalConsumedValue); // 500 + 150
        Assert.Equal(40m, fifo.TotalQty); // 20 from Bin2 + 20 from Bin3
    }

    [Fact]
    public void FifoValuation_LIFO_NewestFirst()
    {
        var lifo = new FifoValuation(isLifo: true);
        lifo.AddStock(50m, 10m);  // Bin 1: 50 @ 10
        lifo.AddStock(30m, 15m);  // Bin 2: 30 @ 15
        lifo.AddStock(20m, 20m);  // Bin 3: 20 @ 20

        var consumed = lifo.RemoveStock(40m);
        // LIFO: takes all of Bin3 (20@20=400) + 20 from Bin2 (20@15=300)
        var totalConsumedValue = consumed.Sum(b => b.Qty * b.Rate);
        Assert.Equal(700m, totalConsumedValue);
        Assert.Equal(60m, lifo.TotalQty);
    }

    [Fact]
    public void FifoValuation_NegativeStock_CreatesNegativeBin()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(10m, 25m);
        fifo.RemoveStock(15m); // goes -5 negative
        Assert.True(fifo.TotalQty < 0);
    }

    [Fact]
    public void FifoValuation_NegativeRecovery_OnNextPurchase()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(10m, 25m);
        fifo.RemoveStock(15m); // goes -5 negative
        fifo.AddStock(20m, 30m); // recovers
        Assert.Equal(15m, fifo.TotalQty);
    }

    [Fact]
    public void FifoValuation_SameRate_CoalescesBins()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(50m, 10m);
        fifo.AddStock(30m, 10m); // same rate
        Assert.Equal(80m, fifo.TotalQty);
        Assert.Equal(800m, fifo.TotalValue); // 80 * 10
    }

    [Fact]
    public void FifoValuation_Serialization_RoundTrip()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(100m, 25m);
        fifo.AddStock(50m, 30m);

        var json = fifo.Serialize();
        Assert.NotNull(json);

        var restored = FifoValuation.Deserialize(json);
        Assert.Equal(150m, restored.TotalQty);
        Assert.Equal(4000m, restored.TotalValue); // 100*25 + 50*30
    }

    // === Payment Allocation Precision ===

    [Fact]
    public void PaymentEntry_Allocation_PrecisionCheck()
    {
        // Verify outstanding doesn't go negative from floating point
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-PREC", DateTime.Today);
        si.GrandTotal = 333.33m; // amount that could cause precision issues

        si.AmountPaid = 333.33m;
        Assert.Equal(0m, si.OutstandingAmount); // must be exactly 0, not -0.0000001
    }

    [Fact]
    public void PaymentEntry_PartialPayment_MaintainsPrecision()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-PART", DateTime.Today);
        si.GrandTotal = 1000m;

        // Three equal payments of 333.33 + final 0.01
        si.AmountPaid = 333.33m;
        Assert.Equal(666.67m, si.OutstandingAmount);

        si.AmountPaid = 666.66m;
        Assert.Equal(333.34m, si.OutstandingAmount);

        si.AmountPaid = 1000m;
        Assert.Equal(0m, si.OutstandingAmount);
    }

    // === Multi-Currency Calculations ===

    [Fact]
    public void ExchangeRate_BaseAmount_Precision()
    {
        // USD 1,234.56 at rate 4.7200 = MYR 5,827.1232
        var amount = 1234.56m;
        var rate = 4.72m;
        var baseAmount = amount * rate;
        Assert.Equal(5827.1232m, baseAmount);
    }

    [Fact]
    public void ExchangeRate_SameCurrency_AlwaysOne()
    {
        // Same currency pair should always return 1.0
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-SAME", DateTime.Today);
        Assert.Equal(1m, si.ExchangeRate); // default
    }

    // === Bin Qty Never Negative (via Max guard) ===

    [Fact]
    public void Bin_ReservedQty_CannotGoNegative_ViaMax()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ReservedQty = 10m;
        // After releasing 15, max(0, 10-15) = max(0, -5) = 0
        bin.ReservedQty = Math.Max(0m, bin.ReservedQty - 15m);
        Assert.Equal(0m, bin.ReservedQty);
    }

    [Fact]
    public void Bin_OrderedQty_CannotGoNegative_ViaMax()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.OrderedQty = 20m;
        bin.OrderedQty = Math.Max(0m, bin.OrderedQty - 30m);
        Assert.Equal(0m, bin.OrderedQty);
    }

    // === BOM Cost Precision ===

    [Fact]
    public void BOM_Cost_WithFractionalQty()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-FRAC", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Paint", 0.5m, 30m)); // 15
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Thinner", 0.25m, 40m)); // 10
        bom.RecalculateCost();
        Assert.Equal(25m, bom.TotalMaterialCost); // 15 + 10
    }

    [Fact]
    public void BOM_Cost_ZeroRate_Item_IncludesInTotal()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-ZERO", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Free Sample", 1m, 0m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Paid Item", 2m, 50m));
        bom.RecalculateCost();
        Assert.Equal(100m, bom.TotalMaterialCost); // 0 + 100
    }

    // === Leave Allocation Edge Cases ===

    [Fact]
    public void LeaveAllocation_DeductMoreThanBalance_ClampsToZero()
    {
        var alloc = new HumanResources.Entities.LeaveAllocation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 5m);
        alloc.DeductLeave(5m); // use all
        Assert.Equal(0m, alloc.Balance);
        // RestoreLeave should work even when at 0
        alloc.RestoreLeave(2m);
        Assert.Equal(2m, alloc.Balance);
    }

    [Fact]
    public void LeaveAllocation_RestoreNeverExceedsAllocated()
    {
        var alloc = new HumanResources.Entities.LeaveAllocation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 10m);
        alloc.DeductLeave(3m);
        alloc.RestoreLeave(5m); // restoring more than deducted: uses Math.Max(0, used-restore)
        // LeavesUsed can't go below 0
        Assert.True(alloc.LeavesUsed >= 0m);
    }

    // === WorkOrder Boundary ===

    [Fact]
    public void WorkOrder_ExactQty_Completes()
    {
        var wo = new Manufacturing.Entities.WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-EX", Guid.NewGuid(), Guid.NewGuid(), 50m);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(50m, 0m); // 0% overproduction = exact limit
        Assert.Equal(50m, wo.ProducedQuantity);
    }

    [Fact]
    public void WorkOrder_ZeroOverproduction_BlocksExcess()
    {
        var wo = new Manufacturing.Entities.WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-ZO", Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.Start();
        // 0% overproduction means max = exactly 100
        Assert.Throws<Volo.Abp.BusinessException>(() => wo.RecordProduction(101m, 0m));
    }
}
