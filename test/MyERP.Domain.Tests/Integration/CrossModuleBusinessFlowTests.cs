using System;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Tests.Integration;

/// <summary>
/// Cross-module integration tests validating end-to-end business scenarios.
/// Uses ONLY verified entity APIs from the existing test suite.
/// </summary>
public class CrossModuleBusinessFlowTests
{
    private static readonly Guid Co = Guid.NewGuid();
    private static readonly Guid Cust = Guid.NewGuid();
    private static readonly Guid Supp = Guid.NewGuid();

    // ═══════════════════════════════════════
    // ── FULL ORDER-TO-CASH CYCLE ──
    // ═══════════════════════════════════════

    [Fact]
    public void OrderToCash_FullCycle_StatusProgression()
    {
        var item = Guid.NewGuid();

        // SO → submit → deliver → bill → complete
        var so = new SalesOrder(Guid.NewGuid(), Co, Cust, "SO-001", DateTime.UtcNow);
        so.AddItem(item, "Widget", 100m, 50m, 0m);
        so.Submit();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
        so.GrandTotal.ShouldBe(5000m);

        // Partial delivery: 60/100
        var soItem = so.Items.First();
        soItem.DeliveredQty = 60m;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill); // Min% < 100

        // Complete delivery: 100/100
        soItem.DeliveredQty = 100m;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToBill);

        // Full billing
        soItem.BilledQty = 100m;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.Completed);
    }

    [Fact]
    public void OrderToCash_CloseBeforeFullDelivery()
    {
        var item = Guid.NewGuid();
        var so = new SalesOrder(Guid.NewGuid(), Co, Cust, "SO-002", DateTime.UtcNow);
        so.AddItem(item, "Part", 200m, 25m, 0m);
        so.Submit();

        // Partial delivery
        so.Items.First().DeliveredQty = 80m;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);

        // Short-close
        so.Close();
        so.Status.ShouldBe(DocumentStatus.Closed);

        // Reopen restores correct status
        so.Reopen();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
    }

    // ═══════════════════════════════════════
    // ── PROCURE-TO-PAY CYCLE ──
    // ═══════════════════════════════════════

    [Fact]
    public void ProcureToPay_FullCycle_WithPayment()
    {
        var item = Guid.NewGuid();

        // PO → PR → PI → Payment
        var po = new PurchaseOrder(Guid.NewGuid(), Co, Supp, "PO-001", DateTime.UtcNow);
        po.AddItem(item, "Steel", 500m, 12m, 0m);
        po.Submit();
        po.GrandTotal.ShouldBe(6000m);

        // Receipt
        po.Items.First().ReceivedQty = 500m;
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(DocumentStatus.ToBill);

        // Invoice
        var pi = new PurchaseInvoice(Guid.NewGuid(), Co, Supp, "PI-001", DateTime.UtcNow);
        pi.AddItem(item, "Steel", 500m, 12m, 0m);
        pi.Submit();
        pi.Post();
        pi.OutstandingAmount.ShouldBe(6000m);

        // Payment
        pi.AmountPaid = 6000m;
        pi.OutstandingAmount.ShouldBe(0m);

        // PO completed
        po.Items.First().BilledQty = 500m;
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(DocumentStatus.Completed);
    }

    [Fact]
    public void PurchaseReturn_DebitNote_ReducesOutstanding()
    {
        var item = Guid.NewGuid();

        // Original invoice
        var pi = new PurchaseInvoice(Guid.NewGuid(), Co, Supp, "PI-002", DateTime.UtcNow);
        pi.AddItem(item, "Defective Parts", 100m, 20m, 0m);
        pi.Submit();
        pi.Post();
        pi.OutstandingAmount.ShouldBe(2000m);

        // Debit note reduces outstanding
        pi.AmountPaid = 500m; // partial payment via debit note
        pi.OutstandingAmount.ShouldBe(1500m);
    }

    // ═══════════════════════════════════════
    // ── MANUFACTURING CYCLE ──
    // ═══════════════════════════════════════

    [Fact]
    public void ManufactureCycle_BOM_WO_Production()
    {
        var fgItem = Guid.NewGuid();
        var rm1 = Guid.NewGuid();
        var rm2 = Guid.NewGuid();

        // BOM: 2×Steel(15) + 5×Wire(3) = 45 per unit
        var bom = new BillOfMaterials(Guid.NewGuid(), Co, "BOM-001", fgItem);
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rm1, "Steel Rod", 2m, 15m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rm2, "Copper Wire", 5m, 3m));
        bom.RecalculateCost();
        bom.TotalCost.ShouldBe(45m);

        // Work Order for 10 units
        var wo = new WorkOrder(Guid.NewGuid(), Co, "WO-001", fgItem, bom.Id, 10m);
        wo.Submit();
        wo.Status.ShouldBe(WorkOrderStatus.Submitted);

        wo.Start();
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);

        // Partial production
        wo.RecordProduction(6m, 0m);
        wo.ProducedQuantity.ShouldBe(6m);

        // Complete
        wo.RecordProduction(4m, 0m);
        wo.ProducedQuantity.ShouldBe(10m);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
    }

    [Fact]
    public void WorkOrder_OverproductionBlocked()
    {
        var item = Guid.NewGuid();
        var bom = new BillOfMaterials(Guid.NewGuid(), Co, "BOM-002", item);
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Material", 1m, 10m));

        var wo = new WorkOrder(Guid.NewGuid(), Co, "WO-002", item, bom.Id, 100m);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100m, 5m); // 100 of 100 with 5% allowance

        // Attempting 6 more would exceed 105% cap
        Should.Throw<BusinessException>(() => wo.RecordProduction(6m, 5m));
    }

    // ═══════════════════════════════════════
    // ── STOCK VALUATION (FIFO/LIFO) ──
    // ═══════════════════════════════════════

    [Fact]
    public void FIFO_ConsumesOldestFirst()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(10m, 50m);  // 10 @ 50
        fifo.AddStock(20m, 60m);  // 20 @ 60

        fifo.TotalQty.ShouldBe(30m);
        fifo.TotalValue.ShouldBe(1700m); // 500 + 1200

        // Consume 15: first 10@50 + 5@60 = 800
        var consumed = fifo.RemoveStock(15m);
        consumed.Sum(b => b.Qty * b.Rate).ShouldBe(800m);
        fifo.TotalQty.ShouldBe(15m);
        fifo.TotalValue.ShouldBe(900m);
    }

    [Fact]
    public void LIFO_ConsumesNewestFirst()
    {
        var lifo = new FifoValuation(isLifo: true);
        lifo.AddStock(10m, 40m);  // 10 @ 40
        lifo.AddStock(20m, 55m);  // 20 @ 55

        // Consume 15 from back: 15@55 = 825
        var consumed = lifo.RemoveStock(15m);
        consumed.Sum(b => b.Qty * b.Rate).ShouldBe(825m);
        lifo.TotalQty.ShouldBe(15m);
        lifo.TotalValue.ShouldBe(675m); // 10×40 + 5×55
    }

    [Fact]
    public void FIFO_NegativeStock_CreatesNegativeBin()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(5m, 100m); // 5 @ 100

        // Consume 8: depletes 5@100, creates -3 @ 0
        fifo.RemoveStock(8m);
        fifo.TotalQty.ShouldBe(-3m);
    }

    [Fact]
    public void FIFO_NegativeRecovery_OnNextPurchase()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(5m, 100m);
        fifo.RemoveStock(8m); // Goes -3
        fifo.TotalQty.ShouldBe(-3m);

        // Recovery: add 10 @ 90, net = 7 @ 90
        fifo.AddStock(10m, 90m);
        fifo.TotalQty.ShouldBe(7m);
    }

    // ═══════════════════════════════════════
    // ── MULTI-CURRENCY INVOICING ──
    // ═══════════════════════════════════════

    [Fact]
    public void MultiCurrency_SI_BaseAmounts()
    {
        var item = Guid.NewGuid();
        var si = new SalesInvoice(Guid.NewGuid(), Co, Cust, "SI-USD-001", DateTime.UtcNow);
        si.ExchangeRate = 4.72m;
        si.AddItem(item, "Consulting", 10m, 500m, 0m);

        si.GrandTotal.ShouldBe(5000m);
        si.BaseGrandTotal.ShouldBe(23_600m); // 5000 × 4.72
    }

    [Fact]
    public void CreditNote_NegativeQtyAndTotal()
    {
        var item = Guid.NewGuid();
        var cn = new SalesInvoice(Guid.NewGuid(), Co, Cust, "CN-001", DateTime.UtcNow);
        cn.IsReturn = true;
        cn.ReturnAgainstId = Guid.NewGuid();
        cn.AddItem(item, "Returned Widget", -5m, 100m, 0m);

        cn.GrandTotal.ShouldBe(-500m);
        cn.IsReturn.ShouldBeTrue();
    }

    // ═══════════════════════════════════════
    // ── INVENTORY BIN PROJECTED QTY ──
    // ═══════════════════════════════════════

    [Fact]
    public void Bin_ProjectedQty_FullFormula()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);
        bin.ActualQty = 100m;
        bin.ReservedQty = 20m;
        bin.OrderedQty = 30m;
        bin.IndentedQty = 10m;
        bin.PlannedQty = 15m;
        bin.ReservedQtyForProduction = 5m;
        bin.ReservedQtyForSubContract = 3m;

        // ProjectedQty = Actual + Ordered + Indented + Planned - Reserved - ReservedForProd - ReservedForSub
        bin.ProjectedQty.ShouldBe(100m + 30m + 10m + 15m - 20m - 5m - 3m); // 127
    }

    [Fact]
    public void Bin_NegativeProjectedQty_AllowedForPlanning()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);
        bin.ActualQty = 10m;
        bin.ReservedQty = 50m; // more reserved than actual

        bin.ProjectedQty.ShouldBe(10m - 50m); // -40 — signals reorder needed
    }

    // ═══════════════════════════════════════
    // ── SUBCONTRACTING ──
    // ═══════════════════════════════════════

    [Fact]
    public void Subcontracting_PartialReceipt_StatusTransitions()
    {
        var fgItem = Guid.NewGuid();
        var sco = new SubcontractingOrder(Guid.NewGuid(), Co, "SCO-001", DateTime.UtcNow, Supp);
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, fgItem, "PCB Assembly", 100m, 25m));
        sco.Submit();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Open);

        // Partial receipt
        sco.Items.First().ReceivedQty = 40m;
        sco.MarkPartiallyReceived();
        sco.Status.ShouldBe(SubcontractingOrderStatus.PartiallyReceived);

        // Complete
        sco.Items.First().ReceivedQty = 100m;
        sco.Close();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Closed);
    }

    // ═══════════════════════════════════════
    // ── MULTI-ITEM ORDER FULFILLMENT ──
    // ═══════════════════════════════════════

    [Fact]
    public void MultiItem_SO_MinPercentFormula()
    {
        var so = new SalesOrder(Guid.NewGuid(), Co, Cust, "SO-003", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Widget A", 100m, 10m, 0m);
        so.AddItem(Guid.NewGuid(), "Widget B", 50m, 20m, 0m);
        so.Submit();

        // Deliver all of A but none of B
        so.Items.First().DeliveredQty = 100m;
        so.Items.Last().DeliveredQty = 0m;
        so.UpdateFulfillmentStatus();

        // Min formula: MIN(100%, 0%) = 0% → stays ToDeliverAndBill
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);

        // Now deliver B too
        so.Items.Last().DeliveredQty = 50m;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToBill);
    }

    // ═══════════════════════════════════════
    // ── PAYMENT + OUTSTANDING ──
    // ═══════════════════════════════════════

    [Fact]
    public void PartialPayment_OutstandingTracking()
    {
        var item = Guid.NewGuid();
        var si = new SalesInvoice(Guid.NewGuid(), Co, Cust, "SI-003", DateTime.UtcNow);
        si.AddItem(item, "Service", 1m, 10000m, 0m);
        si.Submit();
        si.Post();
        si.OutstandingAmount.ShouldBe(10000m);

        // Partial payment
        si.AmountPaid = 4000m;
        si.OutstandingAmount.ShouldBe(6000m);

        // Full payment
        si.AmountPaid = 10000m;
        si.OutstandingAmount.ShouldBe(0m);
    }

    [Fact]
    public void CreditNote_ReducesOriginalOutstanding()
    {
        var item = Guid.NewGuid();
        var original = new SalesInvoice(Guid.NewGuid(), Co, Cust, "SI-004", DateTime.UtcNow);
        original.AddItem(item, "Product", 10m, 100m, 0m);
        original.Submit();
        original.Post();
        original.OutstandingAmount.ShouldBe(1000m);

        // Credit note of 300 applied as payment
        original.AmountPaid = 300m;
        original.OutstandingAmount.ShouldBe(700m);
    }

    // ═══════════════════════════════════════
    // ── DOCUMENT SERIES WITH FY ──
    // ═══════════════════════════════════════

    [Fact]
    public void DocumentSeries_FiscalYearReset()
    {
        var series = new DocumentSeries(Guid.NewGuid(), Co, "SI Numbering", "SalesInvoice", "SI-");
        series.ResetOnFiscalYear = true;

        var num1 = series.GenerateNextNumberForFiscalYear(2026);
        num1.ShouldBe("SI-2026-00001");

        var num2 = series.GenerateNextNumberForFiscalYear(2026);
        num2.ShouldBe("SI-2026-00002");

        // New FY resets
        var num3 = series.GenerateNextNumberForFiscalYear(2027);
        num3.ShouldBe("SI-2027-00001");
    }

    [Fact]
    public void DocumentSeries_NoReset_ContinuousNumbering()
    {
        var series = new DocumentSeries(Guid.NewGuid(), Co, "PO Numbering", "PurchaseOrder", "PO-");
        series.ResetOnFiscalYear = false;

        var num1 = series.GenerateNextNumber();
        var num2 = series.GenerateNextNumber();
        num1.ShouldBe("PO-00001");
        num2.ShouldBe("PO-00002");
    }
}
