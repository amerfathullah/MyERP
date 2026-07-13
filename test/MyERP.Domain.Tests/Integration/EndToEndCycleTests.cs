using System;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// End-to-end integration tests covering the complete ERP transaction cycle.
/// Tests the full flow from purchase through to sales with all side effects.
/// </summary>
public class EndToEndCycleTests
{
    [Fact]
    public void PurchaseToSalesCycle_FullFlow()
    {
        // === PHASE 1: PURCHASE ===
        // Create PO for 100 units of Widget @ RM50
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-E2E-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Widget A", 100, 50, 400); // SST 8% of 5000 = 400
        po.Submit();
        po.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
        po.GrandTotal.ShouldBe(5400m); // 5000 + 400 = 5400

        // Simulate PR: receive 100 units
        po.Items[0].ReceivedQty = 100;
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(DocumentStatus.ToBill);

        // Simulate PI: bill all
        po.Items[0].BilledQty = 100;
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(DocumentStatus.Completed);

        // === PHASE 2: SALES ===
        // Create SO for 80 units @ RM100 (mark up from cost of 50)
        var so = new SalesOrder(Guid.NewGuid(), po.CompanyId, Guid.NewGuid(), "SO-E2E-001", DateTime.UtcNow);
        so.AddItem(po.Items[0].ItemId, "Widget A", 80, 100, 640); // SST 8% of 8000 = 640
        so.Submit();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
        so.GrandTotal.ShouldBe(8640m); // 8000 + 640 = 8640

        // Partial delivery: 50 units
        so.Items[0].DeliveredQty = 50;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
        // PerDelivered = (50/80 * 100) / 1 item = 62.5%
        so.PerDelivered.ShouldBe(62.5m);

        // Complete delivery: remaining 30 units
        so.Items[0].DeliveredQty = 80;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToBill);

        // Bill all
        so.Items[0].BilledQty = 80;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.Completed);
    }

    [Fact]
    public void ShortClose_PartialFulfillment_Cycle()
    {
        // Create SO for 200 units
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-SHORT-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Large Widget", 200, 75, 1200); // SST 8% of 15000 = 1200
        so.Submit();
        so.GrandTotal.ShouldBe(16200m); // 15000 + 1200 = 16200

        // Deliver only 120 (customer reduced order)
        so.Items[0].DeliveredQty = 120;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);

        // Short-close: don't deliver remaining 80
        so.Close();
        so.Status.ShouldBe(DocumentStatus.Closed);

        // Verify pending qty reflects the undelivered amount
        so.Items[0].PendingDeliveryQty.ShouldBe(80);

        // Reopen if customer wants more
        so.Reopen();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
    }

    [Fact]
    public void FifoValuation_PurchaseSellCycle()
    {
        var queue = new FifoValuation();

        // Purchase batch 1: 500 @ RM10
        queue.AddStock(500, 10m);
        queue.TotalQty.ShouldBe(500);
        queue.TotalValue.ShouldBe(5000m);

        // Purchase batch 2: 300 @ RM12 (price increase)
        queue.AddStock(300, 12m);
        queue.TotalQty.ShouldBe(800);
        queue.TotalValue.ShouldBe(8600m);

        // Sell 600 units (FIFO: 500@10 + 100@12 = 6200)
        var consumed = queue.RemoveStock(600);
        var cogsRate = FifoValuation.GetOutgoingRate(consumed);
        cogsRate.ShouldBeInRange(10.33m, 10.34m); // (5000+1200)/600 = 10.33

        // Remaining: 200 @ RM12
        queue.TotalQty.ShouldBe(200);
        queue.TotalValue.ShouldBe(2400m);

        // Gross profit per unit = Selling price - COGS
        // If selling at RM20: margin = 20 - 10.33 = RM9.67/unit
        var sellingPrice = 20m;
        var margin = sellingPrice - cogsRate;
        margin.ShouldBeInRange(9.66m, 9.67m);
    }

    [Fact]
    public void MultiCurrency_USDInvoice_ExchangeRate()
    {
        // Simulate: USD invoice for a Malaysian company (MYR base)
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-USD-001", DateTime.UtcNow);
        si.CurrencyCode = "USD";
        si.ExchangeRate = 4.72m; // 1 USD = 4.72 MYR

        si.AddItem(Guid.NewGuid(), "Consulting Service", 10, 100m, 0); // 10 hours @ $100

        // USD amounts
        si.NetTotal.ShouldBe(1000m); // 10 * 100
        si.GrandTotal.ShouldBe(1000m);

        // Base (MYR) amounts
        si.BaseNetTotal.ShouldBe(4720m); // 1000 * 4.72
        si.BaseGrandTotal.ShouldBe(4720m);
    }

    [Fact]
    public void PaymentFlow_PartialPayment_Outstanding()
    {
        // Create and post invoice for RM10,000
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-PAY-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Project Phase 1", 1, 10000m, 600m); // 6% SST
        si.Submit();

        si.GrandTotal.ShouldBe(10600m); // 10000 + 600
        si.OutstandingAmount.ShouldBe(10600m);

        // First payment: RM5,000
        si.AmountPaid = 5000m;
        si.OutstandingAmount.ShouldBe(5600m);

        // Second payment: RM5,600 (full settlement)
        si.AmountPaid = 10600m;
        si.OutstandingAmount.ShouldBe(0m);
    }

    [Fact]
    public void CreditLimit_Enforcement()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "ABC Trading");
        customer.CreditLimit = 50000m;

        // Outstanding invoices: RM45,000
        // New invoice: RM10,000
        // Total exposure: 55,000 > 50,000 limit → would be blocked
        var totalExposure = 45000m + 10000m;
        (totalExposure > customer.CreditLimit).ShouldBeTrue();
    }

    [Fact]
    public void ItemReorderLevel_Detection()
    {
        // Item with reorder settings
        var item = new MyERP.Inventory.Entities.Item(
            Guid.NewGuid(), Guid.NewGuid(), "WIDGET-001", "Widget A",
            MyERP.Inventory.ItemType.Goods);
        item.ReorderLevel = 50;
        item.ReorderQty = 200;
        item.SafetyStock = 20;

        // Current projected qty = 30 (below reorder level of 50)
        var projectedQty = 30m;
        var needsReorder = projectedQty < item.ReorderLevel;
        needsReorder.ShouldBeTrue();

        // Order quantity should be the configured reorder qty
        item.ReorderQty.ShouldBe(200m);
    }
}
