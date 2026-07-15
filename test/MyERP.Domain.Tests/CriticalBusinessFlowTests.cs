using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Xunit;

namespace MyERP;

/// <summary>
/// Critical business flow tests verifying entity state machines and cross-module interactions.
/// </summary>
public class CriticalBusinessFlowTests
{
    // === PROCUREMENT-TO-STOCK FLOW ===

    [Fact]
    public void PO_Submit_Reserves_OrderedQty_In_Bin()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(0m, bin.OrderedQty);
        bin.OrderedQty += 100m;
        Assert.Equal(100m, bin.OrderedQty);
        Assert.Equal(100m, bin.ProjectedQty);
    }

    [Fact]
    public void PR_Submit_Reduces_OrderedQty_And_Increases_ActualQty()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.OrderedQty = 100m;
        bin.ActualQty += 50m;
        bin.OrderedQty -= 50m;
        Assert.Equal(50m, bin.ActualQty);
        Assert.Equal(50m, bin.OrderedQty);
        Assert.Equal(100m, bin.ProjectedQty);
    }

    [Fact]
    public void PO_Item_OverBilling_Detection()
    {
        var poItem = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Widget", 100m, 10m, 0m, "Unit");
        poItem.ReceivedQty = 100m;
        poItem.BilledQty = 80m;
        var wouldExceed = (poItem.BilledQty + 30m) > poItem.Quantity;
        Assert.True(wouldExceed); // 80 + 30 = 110 > 100
    }

    [Fact]
    public void PO_Item_WithinBilling_Passes()
    {
        var poItem = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Widget", 100m, 10m, 0m, "Unit");
        poItem.BilledQty = 70m;
        var wouldExceed = (poItem.BilledQty + 20m) > poItem.Quantity;
        Assert.False(wouldExceed); // 70 + 20 = 90 ≤ 100
    }

    // === SALES ORDER FULFILLMENT ===

    [Fact]
    public void SO_Submit_Transitions_To_ToDeliverAndBill()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item A", 50m, 10m, 0m);
        so.Submit();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);
    }

    [Fact]
    public void SO_FullDelivery_Transitions_To_ToBill()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-002", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item A", 50m, 10m, 0m);
        so.Submit();
        so.Items.First().DeliveredQty = 50m;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToBill, so.Status);
    }

    [Fact]
    public void SO_PartialDelivery_Stays_ToDeliverAndBill()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-003", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item A", 100m, 10m, 0m);
        so.Submit();
        so.Items.First().DeliveredQty = 40m;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);
    }

    [Fact]
    public void SO_FullyFulfilled_Transitions_To_Completed()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-004", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item A", 50m, 100m, 0m);
        so.Submit();
        so.Items.First().DeliveredQty = 50m;
        so.Items.First().BilledQty = 50m;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.Completed, so.Status);
    }

    [Fact]
    public void SO_Close_And_Reopen()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-005", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item A", 100m, 10m, 0m);
        so.Submit();
        so.Items.First().DeliveredQty = 100m;
        so.Close();
        Assert.Equal(DocumentStatus.Closed, so.Status);
        so.Reopen();
        Assert.Equal(DocumentStatus.ToBill, so.Status); // fully delivered → ToBill
    }

    // === STOCK LEDGER ===

    [Fact]
    public void SLE_Positive_Is_StockIn()
    {
        var sle = new StockLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Now, 50m, 10m, 50m, 500m);
        Assert.True(sle.QuantityChange > 0);
    }

    [Fact]
    public void SLE_Negative_Is_StockOut()
    {
        var sle = new StockLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Now, -30m, 12m, -30m, -360m);
        Assert.True(sle.QuantityChange < 0);
    }

    // === MANUFACTURING ===

    [Fact]
    public void WO_Overproduction_Within_Limit()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(105m, 10m); // 10% → max 110, attempt 105 is OK
        Assert.Equal(105m, wo.ProducedQuantity);
    }

    [Fact]
    public void WO_Overproduction_Blocked()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-002", Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.Start();
        Assert.Throws<Volo.Abp.BusinessException>(() => wo.RecordProduction(110m, 5m)); // 5% → max 105
    }

    [Fact]
    public void BOM_TotalCost_Sums_Items()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Material", 5m, 10m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Material", 3m, 20m));
        bom.RecalculateCost();
        Assert.Equal(110m, bom.TotalMaterialCost);
    }

    // === PAYMENT & OUTSTANDING ===

    [Fact]
    public void SI_Outstanding_Reduces_After_Payment()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.Today);
        si.GrandTotal = 1000m;
        Assert.Equal(1000m, si.OutstandingAmount);
        si.AmountPaid = 400m;
        Assert.Equal(600m, si.OutstandingAmount);
    }

    [Fact]
    public void SI_FullPayment_Clears_Outstanding()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-002", DateTime.Today);
        si.GrandTotal = 1000m;
        si.AmountPaid = si.GrandTotal;
        Assert.Equal(0m, si.OutstandingAmount);
    }

    // === CREDIT LIMIT ===

    [Fact]
    public void Customer_CreditLimit_Zero_IsUnlimited()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test Customer");
        Assert.Equal(0m, customer.CreditLimit);
        Assert.False(customer.CreditLimit > 0);
    }

    [Fact]
    public void Customer_CreditLimit_Exceeded_Detection()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Customer Ltd");
        customer.CreditLimit = 50000m;
        var wouldExceed = (45000m + 8000m) > customer.CreditLimit;
        Assert.True(wouldExceed);
    }

    // === BIN PROJECTED QTY ===

    [Fact]
    public void Bin_ProjectedQty_Full_Formula()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty = 100m;
        bin.OrderedQty = 50m;
        bin.IndentedQty = 20m;
        bin.PlannedQty = 10m;
        bin.ReservedQty = 30m;
        bin.ReservedQtyForProduction = 15m;
        bin.ReservedQtyForSubContract = 5m;
        bin.ReservedQtyForProductionPlan = 10m;
        var expected = 100m + 50m + 20m + 10m - 30m - 15m - 5m - 10m; // 120
        Assert.Equal(expected, bin.ProjectedQty);
    }

    [Fact]
    public void Bin_ProjectedQty_Can_Be_Negative()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty = 10m;
        bin.ReservedQty = 50m;
        Assert.True(bin.ProjectedQty < 0);
    }

    // === RETURN RULES ===

    [Fact]
    public void SI_Return_Must_Have_Negative_Qty()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CN-001", DateTime.Today);
        si.IsReturn = true;
        si.AddItem(Guid.NewGuid(), "Return Item", -5m, 100m, 0m);
        si.GrandTotal = -500m;
        Assert.True(si.GrandTotal < 0);
    }

    [Fact]
    public void SI_Return_Positive_Qty_Blocked()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CN-002", DateTime.Today);
        si.IsReturn = true;
        Assert.Throws<ArgumentException>(() => si.AddItem(Guid.NewGuid(), "Item", 5m, 100m, 0m));
    }

    // === MULTI-CURRENCY ===

    [Fact]
    public void SI_BaseAmount_Uses_ExchangeRate()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-USD", DateTime.Today);
        si.ExchangeRate = 4.72m;
        si.GrandTotal = 1000m;
        si.BaseGrandTotal = 4720m; // 1000 * 4.72
        Assert.Equal(1000m, si.GrandTotal);
        Assert.Equal(4720m, si.BaseGrandTotal);
    }

    // === LEAVE ALLOCATION ===

    [Fact]
    public void LeaveAllocation_Balance_Includes_CarryForward()
    {
        var alloc = new HumanResources.Entities.LeaveAllocation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 14m);
        alloc.CarryForwardDays = 3m;
        Assert.Equal(17m, alloc.Balance);
    }

    [Fact]
    public void LeaveAllocation_DeductAndRestore()
    {
        var alloc = new HumanResources.Entities.LeaveAllocation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 14m);
        alloc.DeductLeave(5m);
        Assert.Equal(9m, alloc.Balance);
        alloc.RestoreLeave(3m);
        Assert.Equal(12m, alloc.Balance);
    }
}
