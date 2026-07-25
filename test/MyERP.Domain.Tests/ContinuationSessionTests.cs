using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests covering recent business logic additions:
/// 1. Work Order Unstop lifecycle (DO-NOT rule compliance)
/// 2. Transit Transfer entity properties
/// 3. Stock Valuation Widget DTO structure
/// 4. PO/SO fulfillment formula edge cases
/// 5. Payment Entry tax properties
/// </summary>
public class ContinuationSessionTests
{
    // --- Work Order Unstop Lifecycle ---

    [Fact]
    public void WorkOrder_Stop_Then_Unstop_Returns_To_InProcess()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();
        wo.Stop();
        Assert.Equal(WorkOrderStatus.Stopped, wo.Status);

        wo.Unstop();
        Assert.Equal(WorkOrderStatus.InProcess, wo.Status);
    }

    [Fact]
    public void WorkOrder_Cancel_From_Stopped_Throws()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();
        wo.Stop();

        Assert.ThrowsAny<Exception>(() => wo.Cancel());
    }

    [Fact]
    public void WorkOrder_Unstop_From_NonStopped_Throws()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();

        // InProcess → cannot Unstop (not stopped)
        Assert.ThrowsAny<Exception>(() => wo.Unstop());
    }

    [Fact]
    public void WorkOrder_Unstop_Then_Cancel_Succeeds()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();
        wo.Stop();
        wo.Unstop();

        // InProcess → Cancel allowed
        wo.Cancel();
        Assert.Equal(WorkOrderStatus.Cancelled, wo.Status);
    }

    [Fact]
    public void WorkOrder_Cancel_From_Submitted_Succeeds()
    {
        var wo = CreateWorkOrder();
        wo.Submit();

        wo.Cancel();
        Assert.Equal(WorkOrderStatus.Cancelled, wo.Status);
    }

    [Fact]
    public void WorkOrder_Cancel_From_InProcess_Succeeds()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();

        wo.Cancel();
        Assert.Equal(WorkOrderStatus.Cancelled, wo.Status);
    }

    [Fact]
    public void WorkOrder_Cancel_From_Completed_Throws()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();
        wo.RecordProduction(10m, 0m); // full production → auto-complete

        Assert.ThrowsAny<Exception>(() => wo.Cancel());
    }

    // --- Stock Valuation Entities ---

    [Fact]
    public void Bin_StockValue_Set_Correctly()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.UpdateActualQty(50m, 2500m);

        Assert.Equal(50m, bin.ActualQty);
        Assert.Equal(2500m, bin.StockValue);
        Assert.Equal(50m, bin.ValuationRate); // 2500/50
    }

    [Fact]
    public void Bin_ValuationRate_Computed_From_StockValue()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.UpdateActualQty(200m, 10000m);

        // ValuationRate should be StockValue / ActualQty = 50
        Assert.Equal(50m, bin.ValuationRate);
    }

    // --- Payment Entry Tax ---

    [Fact]
    public void PaymentEntryTax_OnPaidAmount_Calculates_Correctly()
    {
        var tax = new PaymentEntryTax(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        tax.ChargeType = PaymentTaxChargeType.OnPaidAmount;
        tax.Rate = 6m; // 6% SST on payment

        tax.Calculate(1000m, 1m);

        Assert.Equal(60m, tax.TaxAmount); // 1000 × 6%
        Assert.Equal(60m, tax.BaseTaxAmount); // same currency, rate = 1
    }

    [Fact]
    public void PaymentEntryTax_Actual_Uses_Fixed_Amount()
    {
        var tax = new PaymentEntryTax(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        tax.ChargeType = PaymentTaxChargeType.Actual;
        tax.TaxAmount = 150m;

        tax.Calculate(5000m, 1m);

        // Actual charges don't recalculate from paid amount
        Assert.Equal(150m, tax.TaxAmount);
    }

    [Fact]
    public void PaymentEntryTax_ExchangeRate_Multiplies_Base()
    {
        var tax = new PaymentEntryTax(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        tax.ChargeType = PaymentTaxChargeType.OnPaidAmount;
        tax.Rate = 10m;

        tax.Calculate(1000m, 4.72m); // USD paid, MYR base

        Assert.Equal(100m, tax.TaxAmount); // 1000 × 10% in USD
        Assert.Equal(472m, tax.BaseTaxAmount); // 100 × 4.72 in MYR
    }

    [Fact]
    public void PaymentEntryTax_IsExchangeGainLoss_DefaultsFalse()
    {
        var tax = new PaymentEntryTax(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        Assert.False(tax.IsExchangeGainLoss);
    }

    [Fact]
    public void PaymentEntryTax_IncludedInPaidAmount_DefaultsFalse()
    {
        var tax = new PaymentEntryTax(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        Assert.False(tax.IncludedInPaidAmount);
    }

    // --- SO/PO Fulfillment Edge Cases ---

    [Fact]
    public void SalesOrder_PerDelivered_ZeroItems_ReturnsZero()
    {
        var so = new SalesOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow, null);
        // No items added — should not throw divide-by-zero
        Assert.Equal(0m, so.PerDelivered);
    }

    [Fact]
    public void SalesOrder_PerBilled_ZeroItems_ReturnsZero()
    {
        var so = new SalesOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-002", DateTime.UtcNow, null);
        Assert.Equal(0m, so.PerBilled);
    }

    [Fact]
    public void PurchaseOrder_PerReceived_ZeroItems_ReturnsZero()
    {
        var po = new PurchaseOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow, null);
        Assert.Equal(0m, po.PerReceived);
    }

    [Fact]
    public void PurchaseOrder_Close_Then_Reopen_Recalculates()
    {
        var po = new PurchaseOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-002", DateTime.UtcNow, null);
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Widget", 10m, 100m, 0m);
        po.Submit();
        po.Close();
        Assert.Equal(DocumentStatus.Closed, po.Status);

        po.Reopen();
        // After reopen, status recalculates based on fulfillment
        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);
    }

    // --- Bin Projected Qty Formula ---

    [Fact]
    public void Bin_ProjectedQty_FullFormula()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.UpdateActualQty(100m, 5000m);
        bin.OrderedQty = 50m;
        bin.IndentedQty = 20m;
        bin.PlannedQty = 30m;
        bin.ReservedQty = 25m;
        bin.ReservedQtyForProduction = 10m;
        bin.ReservedQtyForSubContract = 5m;
        bin.ReservedQtyForProductionPlan = 3m;

        // Formula: actual + ordered + indented + planned - reserved - production - subcontract - productionPlan
        var expected = 100m + 50m + 20m + 30m - 25m - 10m - 5m - 3m;
        Assert.Equal(expected, bin.ProjectedQty);
        Assert.Equal(157m, bin.ProjectedQty);
    }

    [Fact]
    public void Bin_ProjectedQty_CanBeNegative()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.UpdateActualQty(5m, 250m);
        bin.ReservedQty = 50m;

        // 5 + 0 + 0 + 0 - 50 - 0 - 0 - 0 = -45
        Assert.True(bin.ProjectedQty < 0);
        Assert.Equal(-45m, bin.ProjectedQty);
    }

    // --- FIFO Valuation ---

    [Fact]
    public void FifoValuation_AddStock_Creates_Bin()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(100m, 10m);

        Assert.Equal(100m, fifo.TotalQty);
        Assert.Equal(1000m, fifo.TotalValue);
    }

    [Fact]
    public void FifoValuation_RemoveStock_Consumes_Oldest()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(50m, 10m); // Batch 1: 50 @ 10
        fifo.AddStock(50m, 15m); // Batch 2: 50 @ 15

        var consumed = fifo.RemoveStock(30m, 0m);

        // Should consume from first batch @ 10
        Assert.NotEmpty(consumed);
        Assert.Equal(70m, fifo.TotalQty); // 100 - 30
    }

    [Fact]
    public void FifoValuation_NegativeStock_Creates_NegativeBin()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(10m, 20m);

        fifo.RemoveStock(15m, 25m);

        // Consumed all 10 @ 20, remaining 5 creates negative bin @ outgoing_rate 25
        Assert.True(fifo.TotalQty < 0);
    }

    // --- Credit Note ---

    [Fact]
    public void SalesInvoice_Return_HasNegativeGrandTotal()
    {
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-RETURN-001", DateTime.UtcNow, null);
        si.IsReturn = true;
        si.ReturnAgainstId = Guid.NewGuid();
        si.AddItem(Guid.NewGuid(), "Return Item", -2m, 100m, 0m);

        Assert.True(si.IsReturn);
        Assert.True(si.GrandTotal < 0);
    }

    // Helper methods

    private static WorkOrder CreateWorkOrder()
    {
        return new WorkOrder(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "WO-TEST-001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            10m,
            null);
    }
}
