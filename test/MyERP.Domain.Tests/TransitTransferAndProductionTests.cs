using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Comprehensive tests for transit transfers, production flows, and delivery schedule interactions.
/// Covers the TransitTransferService domain logic + production material requirements + delivery tracking.
/// </summary>
public class TransitTransferAndProductionTests
{
    // ─── Transit Transfer Records ────────────────────────────────────────────

    [Fact]
    public void TransitTransferItem_Record_CreatesCorrectly()
    {
        var item = new TransitTransferItem(Guid.NewGuid(), 100m, 15.50m);
        Assert.Equal(100m, item.Quantity);
        Assert.Equal(15.50m, item.ValuationRate);
    }

    [Fact]
    public void TransitTransferItem_NullValuationRate_Allowed()
    {
        var item = new TransitTransferItem(Guid.NewGuid(), 50m, null);
        Assert.Null(item.ValuationRate);
    }

    [Fact]
    public void PendingTransitTransfer_Record_CreatesCorrectly()
    {
        var id = Guid.NewGuid();
        var pending = new PendingTransitTransfer(
            id, "SE-2026-00042", new DateTime(2026, 7, 23),
            Guid.NewGuid(), 250m, 5);
        Assert.Equal(id, pending.StockEntryId);
        Assert.Equal("SE-2026-00042", pending.EntryNumber);
        Assert.Equal(250m, pending.TotalQuantity);
        Assert.Equal(5, pending.ItemCount);
    }

    [Fact]
    public void PendingTransitTransfer_ZeroQuantity_Allowed()
    {
        var pending = new PendingTransitTransfer(
            Guid.NewGuid(), "SE-001", DateTime.Today, Guid.NewGuid(), 0m, 0);
        Assert.Equal(0m, pending.TotalQuantity);
        Assert.Equal(0, pending.ItemCount);
    }

    // ─── StockEntry Entity — Transit Types ───────────────────────────────────

    [Fact]
    public void StockEntry_SendToWarehouse_CreatesCorrectly()
    {
        var companyId = Guid.NewGuid();
        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.SendToWarehouse, DateTime.Today);
        Assert.Equal(StockEntryType.SendToWarehouse, entry.EntryType);
        Assert.Equal(DocumentStatus.Draft, entry.Status);
    }

    [Fact]
    public void StockEntry_ReceiveAtWarehouse_CreatesCorrectly()
    {
        var companyId = Guid.NewGuid();
        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.ReceiveAtWarehouse, DateTime.Today);
        Assert.Equal(StockEntryType.ReceiveAtWarehouse, entry.EntryType);
    }

    [Fact]
    public void StockEntry_ReferenceType_CanBeSentForTransit()
    {
        var entry = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.ReceiveAtWarehouse, DateTime.Today);
        var outgoingId = Guid.NewGuid();
        entry.ReferenceType = "StockEntry";
        entry.ReferenceId = outgoingId;
        Assert.Equal("StockEntry", entry.ReferenceType);
        Assert.Equal(outgoingId, entry.ReferenceId);
    }

    [Fact]
    public void StockEntry_TransitTypes_ExistInEnum()
    {
        Assert.True(Enum.IsDefined(typeof(StockEntryType), StockEntryType.SendToWarehouse));
        Assert.True(Enum.IsDefined(typeof(StockEntryType), StockEntryType.ReceiveAtWarehouse));
    }

    // ─── Work Order Production Tracking ──────────────────────────────────────

    [Fact]
    public void WorkOrder_ProducedQuantity_TracksIncrementally()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(30m, 0m); // no overproduction allowance
        Assert.Equal(30m, wo.ProducedQuantity);

        wo.RecordProduction(40m, 0m);
        Assert.Equal(70m, wo.ProducedQuantity);
    }

    [Fact]
    public void WorkOrder_AutoCompletes_AtFullQuantity()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-002", Guid.NewGuid(), Guid.NewGuid(), 50m);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(50m, 0m);
        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
    }

    [Fact]
    public void WorkOrder_OverproductionWithAllowance_Works()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-003", Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.Start();
        // 10% overproduction = max 110 units
        wo.RecordProduction(105m, 10m);
        Assert.Equal(105m, wo.ProducedQuantity);
        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
    }

    [Fact]
    public void WorkOrder_OverproductionExceedsAllowance_Throws()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-004", Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.Start();
        // 5% allowance = max 105, producing 106 should fail
        Assert.Throws<Volo.Abp.BusinessException>(() => wo.RecordProduction(106m, 5m));
    }

    [Fact]
    public void WorkOrder_PercentComplete_CalculatesCorrectly()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-005", Guid.NewGuid(), Guid.NewGuid(), 200m);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(50m, 0m);
        // 50/200 = 25%
        Assert.Equal(25m, wo.PercentComplete);
    }

    [Fact]
    public void WorkOrder_PercentComplete_ZeroQuantity_NoException()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-006", Guid.NewGuid(), Guid.NewGuid(), 0m);
        // Zero quantity WO → PercentComplete should be 0, not throw divide-by-zero
        Assert.Equal(0m, wo.PercentComplete);
    }

    [Fact]
    public void WorkOrder_MaterialTransfer_TracksTransferredQty()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-007", Guid.NewGuid(), Guid.NewGuid(), 100m);
        var itemId = Guid.NewGuid();
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo.Id, itemId, "Test RM", 50m));
        var item = wo.RequiredItems.First();
        Assert.Equal(0m, item.TransferredQuantity);
    }

    // ─── Delivery Schedule Entry ─────────────────────────────────────────────

    [Fact]
    public void DeliveryScheduleEntry_RecordDelivery_ReducesPending()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 8, 1), 100m);
        entry.RecordDelivery(40m);
        Assert.Equal(40m, entry.DeliveredQty);
        Assert.Equal(60m, entry.PendingQty);
    }

    [Fact]
    public void DeliveryScheduleEntry_FullDelivery_IsComplete()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 8, 1), 100m);
        entry.RecordDelivery(100m);
        Assert.True(entry.IsFullyDelivered);
        Assert.Equal(0m, entry.PendingQty);
    }

    [Fact]
    public void DeliveryScheduleEntry_ProgressiveDelivery_Accumulates()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, 200m);
        entry.RecordDelivery(50m);
        entry.RecordDelivery(75m);
        entry.RecordDelivery(75m);
        Assert.Equal(200m, entry.DeliveredQty);
        Assert.True(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_PendingNeverNegative()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, 50m);
        entry.RecordDelivery(60m); // over-deliver
        Assert.True(entry.PendingQty >= 0m);
    }

    // ─── Sales Order Fulfillment Status ──────────────────────────────────────

    [Fact]
    public void SalesOrder_Submit_SetsToDeliverAndBill()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-TEST", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Widget", 10m, 50m, 0m);
        so.Submit();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);
    }

    [Fact]
    public void SalesOrder_FullyDelivered_ToBill()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-TEST", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Widget", 10m, 50m, 0m);
        so.Submit();
        var item = so.Items.First();
        item.DeliveredQty = 10m;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToBill, so.Status);
    }

    [Fact]
    public void SalesOrder_FullyDeliveredAndBilled_Completed()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-TEST", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Widget", 10m, 50m, 0m);
        so.Submit();
        var item = so.Items.First();
        item.DeliveredQty = 10m;
        item.BilledQty = 10m;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.Completed, so.Status);
    }

    [Fact]
    public void SalesOrder_Close_FromActive()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-TEST", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Widget", 5m, 100m, 0m);
        so.Submit();
        so.Close();
        Assert.Equal(DocumentStatus.Closed, so.Status);
    }

    [Fact]
    public void SalesOrder_Reopen_RecalculatesStatus()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-TEST", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Widget", 10m, 50m, 0m);
        so.Submit();
        var item = so.Items.First();
        item.DeliveredQty = 10m; // fully delivered
        so.Close(); // manually closed
        so.Reopen(); // should recalculate → ToBill (fully delivered, not billed)
        Assert.Equal(DocumentStatus.ToBill, so.Status);
    }

    // ─── Purchase Order Fulfillment ──────────────────────────────────────────

    [Fact]
    public void PurchaseOrder_FullReceiptAndBilling_Completed()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-TEST", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Raw Material", 100m, 25m, 0m);
        po.Submit();
        var item = po.Items.First();
        item.ReceivedQty = 100m;
        item.BilledQty = 100m;
        po.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.Completed, po.Status);
    }

    [Fact]
    public void PurchaseOrder_PartialReceipt_StaysActive()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-TEST", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Material A", 100m, 10m, 0m);
        po.AddItem(Guid.NewGuid(), "Material B", 50m, 20m, 0m);
        po.Submit();
        po.Items.First().ReceivedQty = 100m; // first item fully received
        // second item not received → MIN% formula keeps it active
        po.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);
    }

    // ─── BOM Cost Calculation ────────────────────────────────────────────────

    [Fact]
    public void BOM_RecalculateCost_SumsItemAmounts()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-TEST-001", Guid.NewGuid());
        var bomId = bom.Id;
        bom.Items.Add(new BomItem(Guid.NewGuid(), bomId, Guid.NewGuid(), "Material 1", 5m, 10m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bomId, Guid.NewGuid(), "Material 2", 3m, 20m));
        bom.RecalculateCost();
        Assert.Equal(110m, bom.TotalCost);
    }

    [Fact]
    public void BOM_IsActive_DefaultsTrue()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-TEST-002", Guid.NewGuid());
        Assert.True(bom.IsActive);
    }

    [Fact]
    public void BOM_IsDefault_CanBeSet()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-TEST-003", Guid.NewGuid());
        bom.IsDefault = true;
        Assert.True(bom.IsDefault);
    }

    // ─── FIFO Valuation ──────────────────────────────────────────────────────

    [Fact]
    public void FifoValuation_AddStock_CreatesBin()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(100m, 10m);
        Assert.Equal(100m, fifo.TotalQty);
        Assert.Equal(1000m, fifo.TotalValue);
    }

    [Fact]
    public void FifoValuation_RemoveStock_ConsumesOldestFirst()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(50m, 10m); // oldest at RM 10
        fifo.AddStock(50m, 15m); // newer at RM 15
        fifo.RemoveStock(30m); // should consume from RM 10 bin
        // After removing 30 from first bin: 20@10 + 50@15 remain
        Assert.Equal(70m, fifo.TotalQty);
        Assert.Equal(20m * 10m + 50m * 15m, fifo.TotalValue);
    }

    [Fact]
    public void FifoValuation_RemoveStock_CrossesBins()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(20m, 10m);
        fifo.AddStock(30m, 15m);
        // Remove 30: takes all 20@10 + 10@15 → remaining: 20@15
        fifo.RemoveStock(30m);
        Assert.Equal(20m, fifo.TotalQty);
        Assert.Equal(20m * 15m, fifo.TotalValue);
    }

    [Fact]
    public void FifoValuation_NegativeStock_CreatesNegativeBin()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(10m, 20m);
        fifo.RemoveStock(15m); // 5 units go negative
        Assert.Equal(-5m, fifo.TotalQty);
    }

    [Fact]
    public void FifoValuation_NegativeRecovery_ResetsRate()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(10m, 20m);
        fifo.RemoveStock(15m); // -5 at rate 20
        fifo.AddStock(30m, 25m); // recovery at 25: total should be 25@25
        Assert.Equal(25m, fifo.TotalQty);
        Assert.Equal(25m * 25m, fifo.TotalValue);
    }

    // ─── Credit Note & Payment Outstanding ───────────────────────────────────

    [Fact]
    public void SalesInvoice_CreditNote_HasNegativeGrandTotal()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-TEST", DateTime.Today);
        si.IsReturn = true;
        si.AddItem(Guid.NewGuid(), "Return Item", -5m, 100m, 0m);
        Assert.True(si.GrandTotal < 0);
    }

    [Fact]
    public void SalesInvoice_OutstandingAmount_ReducesWithPayment()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-TEST", DateTime.Today);
        si.AddItem(Guid.NewGuid(), "Item", 1m, 1000m, 0m);
        Assert.Equal(1000m, si.OutstandingAmount);
        si.AmountPaid = 600m;
        Assert.Equal(400m, si.OutstandingAmount);
    }

    [Fact]
    public void SalesInvoice_FullPayment_ZeroOutstanding()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-TEST", DateTime.Today);
        si.AddItem(Guid.NewGuid(), "Item", 2m, 500m, 0m);
        si.AmountPaid = 1000m;
        Assert.Equal(0m, si.OutstandingAmount);
    }

    // ─── Payment Entry Multi-Reference ───────────────────────────────────────

    [Fact]
    public void PaymentEntry_UnallocatedAmount_WithMultipleReferences()
    {
        var peId = Guid.NewGuid();
        var pe = new PaymentEntry(peId, Guid.NewGuid(),
            PaymentType.Receive, DateTime.Today, 10000m, Guid.NewGuid(), Guid.NewGuid());
        pe.References.Add(new PaymentEntryReference(
            Guid.NewGuid(), peId, "SalesInvoice", Guid.NewGuid(), 8000m, 8000m, 6000m));
        pe.References.Add(new PaymentEntryReference(
            Guid.NewGuid(), peId, "SalesInvoice", Guid.NewGuid(), 5000m, 5000m, 3000m));
        Assert.Equal(1000m, pe.UnallocatedAmount);
    }

    [Fact]
    public void PaymentEntry_ExchangeGainLoss_Positive_WhenFavorable()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(),
            PaymentType.Receive, DateTime.Today, 1000m, Guid.NewGuid(), Guid.NewGuid());
        pe.ExchangeRate = 4.80m; // payment rate
        pe.SourceExchangeRate = 4.72m; // invoice rate (lower)
        // Gain = 1000 × (4.80 - 4.72) = 80
        Assert.Equal(80m, pe.ExchangeGainLoss);
    }

    [Fact]
    public void PaymentEntry_ExchangeGainLoss_Negative_WhenUnfavorable()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(),
            PaymentType.Receive, DateTime.Today, 2000m, Guid.NewGuid(), Guid.NewGuid());
        pe.ExchangeRate = 4.50m; // payment rate (lower)
        pe.SourceExchangeRate = 4.72m; // invoice rate (higher)
        // Loss = 2000 × (4.50 - 4.72) = -440
        Assert.Equal(-440m, pe.ExchangeGainLoss);
    }

    [Fact]
    public void PaymentEntry_ExchangeGainLoss_Zero_SameCurrency()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(),
            PaymentType.Receive, DateTime.Today, 5000m, Guid.NewGuid(), Guid.NewGuid());
        pe.ExchangeRate = 1m;
        pe.SourceExchangeRate = 1m;
        Assert.Equal(0m, pe.ExchangeGainLoss);
    }

    // ─── UOM Conversion Propagation ──────────────────────────────────────────

    [Fact]
    public void SalesOrderItem_StockQty_WithConversionFactor()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-TEST", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Product in Dozens", 5m, 120m, 0m); // 5 dozens
        var item = so.Items.First();
        item.ConversionFactor = 12m; // 1 Dozen = 12 Units
        item.StockUom = "Unit";
        Assert.Equal(60m, item.StockQty); // 5 × 12 = 60 stock units
    }

    [Fact]
    public void SalesOrderItem_StockQty_SameUom_FactorOne()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-TEST", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Simple item", 25m, 10m, 0m);
        var item = so.Items.First();
        // Default ConversionFactor = 1
        Assert.Equal(25m, item.StockQty);
    }
}
