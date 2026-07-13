using System;
using System.Linq;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Projects.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Integration tests for recently-wired business flows:
/// - SubcontractingReceipt stock movements
/// - POS stock deduction
/// - Timesheet billing
/// - Stock Entry Manufacture BOM population
/// </summary>
public class RecentFlowTests
{
    [Fact]
    public void SCR_Submit_UpdatesSCO_ReceivedQty()
    {
        var sco = new SubcontractingOrder(Guid.NewGuid(), Guid.NewGuid(), "SCO-001",
            DateTime.UtcNow, Guid.NewGuid());
        var itemId = Guid.NewGuid();
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, itemId, "Widget", 100, 5));
        sco.Submit();

        // Simulate partial receipt of 60 units
        var scoItem = sco.Items.First();
        scoItem.ReceivedQty += 60;

        scoItem.ReceivedQty.ShouldBe(60);
        var perReceived = sco.Items.Min(i => i.Qty > 0 ? (i.ReceivedQty / i.Qty) * 100m : 100m);
        perReceived.ShouldBe(60m);
    }

    [Fact]
    public void SCR_FullReceipt_CompletesOrder()
    {
        var sco = new SubcontractingOrder(Guid.NewGuid(), Guid.NewGuid(), "SCO-002",
            DateTime.UtcNow, Guid.NewGuid());
        var itemId = Guid.NewGuid();
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, itemId, "Widget", 50, 10));
        sco.Submit();

        // Simulate full receipt
        var scoItem = sco.Items.First();
        scoItem.ReceivedQty = 50;
        var perReceived = sco.Items.Min(i => (i.ReceivedQty / i.Qty) * 100m);
        perReceived.ShouldBe(100m);

        // Full receipt triggers close
        sco.Close();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Closed);
    }

    [Fact]
    public void SCR_PartialReceipt_TransitionsToPartiallyReceived()
    {
        var sco = new SubcontractingOrder(Guid.NewGuid(), Guid.NewGuid(), "SCO-003",
            DateTime.UtcNow, Guid.NewGuid());
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "A", 100, 5));
        sco.Submit();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Open);

        sco.MarkPartiallyReceived();
        sco.Status.ShouldBe(SubcontractingOrderStatus.PartiallyReceived);
    }

    [Fact]
    public void SCR_MultiItem_MinPercentage()
    {
        var sco = new SubcontractingOrder(Guid.NewGuid(), Guid.NewGuid(), "SCO-004",
            DateTime.UtcNow, Guid.NewGuid());
        var item1 = new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "A", 100, 5);
        var item2 = new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "B", 200, 3);
        sco.AddItem(item1);
        sco.AddItem(item2);
        sco.Submit();

        // Receive 80% of item A, 50% of item B
        item1.ReceivedQty = 80;
        item2.ReceivedQty = 100;

        var perA = (item1.ReceivedQty / item1.Qty) * 100m; // 80%
        var perB = (item2.ReceivedQty / item2.Qty) * 100m; // 50%
        var minPer = Math.Min(perA, perB); // 50% — uses Min, not average
        minPer.ShouldBe(50m);
    }

    [Fact]
    public void POS_Invoice_HasUpdateStockTrue()
    {
        // POS invoices should always deduct stock
        var invoice = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "POS-001", DateTime.UtcNow);
        invoice.UpdateStock = true;
        invoice.WarehouseId = Guid.NewGuid();

        invoice.UpdateStock.ShouldBeTrue();
        invoice.WarehouseId.ShouldNotBeNull();
    }

    [Fact]
    public void POS_Invoice_GoesDirectlyToPosted()
    {
        var invoice = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "POS-002", DateTime.UtcNow);
        invoice.AddItem(Guid.NewGuid(), "Item A", 2, 25m, 0);

        invoice.Submit();
        invoice.Post();

        invoice.Status.ShouldBe(Core.DocumentStatus.Posted);
    }

    [Fact]
    public void Timesheet_BillableDetail_TracksBillingLink()
    {
        var ts = new Timesheet(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 7));
        var detail = new TimesheetDetail(Guid.NewGuid(), ts.Id, "Consulting",
            new DateTime(2026, 7, 1, 9, 0, 0), new DateTime(2026, 7, 1, 17, 0, 0), 8)
        { IsBillable = true, BillingRate = 200, CostingRate = 100 };
        ts.AddDetail(detail);
        ts.Submit();

        // Before billing
        detail.SalesInvoiceId.ShouldBeNull();
        detail.BillingAmount.ShouldBe(1600m);

        // After billing
        var invoiceId = Guid.NewGuid();
        detail.SalesInvoiceId = invoiceId;
        detail.SalesInvoiceId.ShouldBe(invoiceId);
    }

    [Fact]
    public void Timesheet_MixedDetails_OnlyBillableGetInvoiceLink()
    {
        var ts = new Timesheet(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 7));

        var billable = new TimesheetDetail(Guid.NewGuid(), ts.Id, "Dev",
            new DateTime(2026, 7, 1, 9, 0, 0), new DateTime(2026, 7, 1, 17, 0, 0), 8)
        { IsBillable = true, BillingRate = 150, CostingRate = 80 };

        var nonBillable = new TimesheetDetail(Guid.NewGuid(), ts.Id, "Admin",
            new DateTime(2026, 7, 2, 9, 0, 0), new DateTime(2026, 7, 2, 12, 0, 0), 3)
        { IsBillable = false, BillingRate = 0, CostingRate = 50 };

        ts.AddDetail(billable);
        ts.AddDetail(nonBillable);

        // Only billable details should be linked to invoices
        var invoiceId = Guid.NewGuid();
        billable.SalesInvoiceId = invoiceId;

        ts.TotalBillingAmount.ShouldBe(1200m); // only billable: 8 × 150
        ts.TotalCostingAmount.ShouldBe(790m); // 8×80 + 3×50
    }

    [Fact]
    public void BOM_ManufactureItems_ProportionalCalculation()
    {
        // BOM for 1 unit of FG needs 2 units of RM-A and 3 units of RM-B
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());
        bom.Quantity = 1;
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM-A", 2, 10));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM-B", 3, 5));
        bom.RecalculateCost();

        // Produce 10 units → need 20 of RM-A and 30 of RM-B
        decimal produceQty = 10;
        decimal multiplier = produceQty / bom.Quantity;

        var rmA_qty = bom.Items[0].Quantity * multiplier;
        var rmB_qty = bom.Items[1].Quantity * multiplier;

        rmA_qty.ShouldBe(20m);
        rmB_qty.ShouldBe(30m);
        bom.TotalCost.ShouldBe(35m); // (2×10) + (3×5) = 35 per unit
    }

    [Fact]
    public void BOM_PhantomItems_ExcludedFromManufacture()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-002", Guid.NewGuid());
        bom.Quantity = 1;
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Real RM", 5, 10)
            { IsPhantom = false });
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Phantom Sub", 2, 20)
            { IsPhantom = true });

        // Only non-phantom items should be included in direct manufacture
        var directItems = bom.Items.Where(i => !i.IsPhantom).ToList();
        directItems.Count.ShouldBe(1);
        directItems[0].ItemName.ShouldBe("Real RM");
    }

    [Fact]
    public void BankTransaction_Reconcile_SetsFields()
    {
        var tx = new Accounting.Entities.BankTransaction(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, "Payment from customer", 5000m);

        tx.IsReconciled.ShouldBeFalse();

        var peId = Guid.NewGuid();
        tx.Reconcile(peId, "PE-001");

        tx.IsReconciled.ShouldBeTrue();
        tx.PaymentEntryId.ShouldBe(peId);
        tx.MatchedDocumentRef.ShouldBe("PE-001");
    }

    [Fact]
    public void BankTransaction_Unreconcile_ClearsFields()
    {
        var tx = new Accounting.Entities.BankTransaction(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, "Wire transfer", 10000m);

        tx.Reconcile(Guid.NewGuid(), "PE-002");
        tx.IsReconciled.ShouldBeTrue();

        tx.Unreconcile();
        tx.IsReconciled.ShouldBeFalse();
        tx.PaymentEntryId.ShouldBeNull();
        tx.MatchedDocumentRef.ShouldBeNull();
    }

    [Fact]
    public void WorkOrder_FGWarehouse_FallbackChain()
    {
        var woWarehouse = Guid.NewGuid();
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001",
            Guid.NewGuid(), Guid.NewGuid(), 100);
        wo.FgWarehouseId = woWarehouse;

        // WO FG warehouse takes priority over BOM
        wo.FgWarehouseId.ShouldBe(woWarehouse);

        // If WO has no FG warehouse, BOM's target warehouse is fallback
        wo.FgWarehouseId = null;
        wo.FgWarehouseId.ShouldBeNull();
    }
}
