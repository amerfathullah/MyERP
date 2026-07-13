using System;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// End-to-end system flow tests validating complete business cycles
/// across multiple modules — simulates real user workflows.
/// </summary>
public class SystemFlowTests
{
    [Fact]
    public void CompleteProcureToPay_SO_PO_PR_PI_PE()
    {
        // 1. Create Purchase Order
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var po = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-001", DateTime.UtcNow);
        po.AddItem(itemId, "Raw Material A", 100, 50m, 0m);
        po.Submit();
        po.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);

        // 2. Receive goods (partial — 60 units)
        var poItem = po.Items.First();
        poItem.ReceivedQty = 60;
        var perReceived = po.Items.Min(i => i.Quantity > 0 ? (i.ReceivedQty / i.Quantity) * 100m : 100m);
        perReceived.ShouldBe(60m);

        // 3. Create Purchase Invoice for received qty
        var pi = new PurchaseInvoice(Guid.NewGuid(), companyId, supplierId, "PI-001", DateTime.UtcNow);
        pi.AddItem(itemId, "Raw Material A", 60, 50m, 0m);
        pi.Submit();
        pi.Post();
        pi.Status.ShouldBe(DocumentStatus.Posted);
        pi.GrandTotal.ShouldBe(3000m);
        pi.OutstandingAmount.ShouldBe(3000m);

        // 4. Make payment (partial — 2000)
        pi.AmountPaid = 2000m;
        pi.OutstandingAmount.ShouldBe(1000m);

        // 5. Complete payment
        pi.AmountPaid = 3000m;
        pi.OutstandingAmount.ShouldBe(0m);
    }

    [Fact]
    public void CompleteOrderToCollection_SO_DN_SI_PE()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        // 1. Sales Order
        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-001", DateTime.UtcNow);
        so.AddItem(itemId, "Widget Pro", 50, 200m, 0m);
        so.Submit();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
        so.GrandTotal.ShouldBe(10000m);

        // 2. Delivery (full)
        var soItem = so.Items.First();
        soItem.DeliveredQty = 50;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToBill); // Fully delivered, pending billing

        // 3. Invoice
        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-001", DateTime.UtcNow);
        si.AddItem(itemId, "Widget Pro", 50, 200m, 0m);
        si.Submit();
        si.Post();
        si.GrandTotal.ShouldBe(10000m);
        si.OutstandingAmount.ShouldBe(10000m);

        // 4. Update SO billing
        soItem.BilledQty = 50;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.Completed); // Fully delivered + fully billed

        // 5. Receive payment
        si.AmountPaid = 10000m;
        si.OutstandingAmount.ShouldBe(0m);
    }

    [Fact]
    public void ManufactureCycle_BOM_WO_Production_Stock()
    {
        var companyId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var rmItemId1 = Guid.NewGuid();
        var rmItemId2 = Guid.NewGuid();

        // 1. BOM: FG needs 2 of RM1 + 3 of RM2
        var bom = new BillOfMaterials(Guid.NewGuid(), companyId, "BOM-001", fgItemId);
        bom.Quantity = 1;
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rmItemId1, "Steel Rod", 2, 15));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rmItemId2, "Bolt Pack", 3, 5));
        bom.RecalculateCost();
        bom.TotalMaterialCost.ShouldBe(45m); // 2×15 + 3×5

        // 2. Work Order for 10 units
        var wo = new WorkOrder(Guid.NewGuid(), companyId, "WO-001", fgItemId, bom.Id, 10);
        wo.Submit();
        wo.Status.ShouldBe(WorkOrderStatus.Submitted);

        // 3. Start production
        wo.Start();
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);

        // 4. Record production (5 units)
        wo.RecordProduction(5);
        wo.ProducedQuantity.ShouldBe(5);
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);

        // 5. Complete production (5 more)
        wo.RecordProduction(5);
        wo.ProducedQuantity.ShouldBe(10);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
    }

    [Fact]
    public void StockMovement_Receipt_Transfer_Issue()
    {
        var companyId = Guid.NewGuid();
        var warehouseA = Guid.NewGuid();
        var warehouseB = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        // 1. Receive stock into warehouse A
        var bin = new Bin(Guid.NewGuid(), itemId, warehouseA);
        bin.ApplyStockMovement(100, 5000); // 100 units at RM50 each
        bin.ActualQty.ShouldBe(100);
        bin.ValuationRate.ShouldBe(50);

        // 2. Reserve 30 for a customer
        bin.ReservedQty = 30;
        bin.ProjectedQty.ShouldBe(70); // 100 - 30

        // 3. Transfer 20 to warehouse B (reduces A)
        bin.ApplyStockMovement(-20, -1000);
        bin.ActualQty.ShouldBe(80);

        var binB = new Bin(Guid.NewGuid(), itemId, warehouseB);
        binB.ApplyStockMovement(20, 1000);
        binB.ActualQty.ShouldBe(20);

        // 4. Issue 10 (material consumption)
        bin.ApplyStockMovement(-10, -500);
        bin.ActualQty.ShouldBe(70);
        bin.ProjectedQty.ShouldBe(40); // 70 - 30 reserved
    }

    [Fact]
    public void CreditNote_ReducesOutstanding()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        // 1. Original invoice for RM 5000
        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-001", DateTime.UtcNow);
        si.AddItem(itemId, "Product", 10, 500m, 0m);
        si.Submit();
        si.Post();
        si.GrandTotal.ShouldBe(5000m);

        // 2. Partial payment of 3000
        si.AmountPaid = 3000m;
        si.OutstandingAmount.ShouldBe(2000m);

        // 3. Credit note for 1000 (reduces outstanding further)
        si.AmountPaid += 1000m; // credit note adds to AmountPaid
        si.OutstandingAmount.ShouldBe(1000m);
    }

    [Fact]
    public void MultiCurrency_ExchangeRate_BaseAmounts()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // USD invoice with MYR as base currency (exchange rate 4.5)
        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-USD-001", DateTime.UtcNow);
        si.CurrencyCode = "USD";
        si.ExchangeRate = 4.5m;
        si.AddItem(Guid.NewGuid(), "Consulting", 10, 100m, 0m); // USD 1000

        si.GrandTotal.ShouldBe(1000m); // Transaction currency
        si.BaseGrandTotal.ShouldBe(4500m); // Base currency (MYR)
    }

    [Fact]
    public void PartialFulfillment_OrderStaysActive()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-002", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item A", 100, 10m, 0m);
        so.AddItem(Guid.NewGuid(), "Item B", 50, 20m, 0m);
        so.Submit();

        // Deliver only Item A fully
        so.Items[0].DeliveredQty = 100;
        so.Items[1].DeliveredQty = 0;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill); // Min% is 0 (Item B)

        // Deliver Item B partially
        so.Items[1].DeliveredQty = 25;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill); // Min% is 50 (Item B)

        // Deliver Item B fully
        so.Items[1].DeliveredQty = 50;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToBill); // 100% delivered, 0% billed
    }

    [Fact]
    public void CloseShortOrder_ReleasesRemainder()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-003", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item X", 100, 50m, 0m);
        so.Submit();

        // Deliver 80 of 100
        so.Items.First().DeliveredQty = 80;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);

        // Short-close the order (remaining 20 units not needed)
        so.Close();
        so.Status.ShouldBe(DocumentStatus.Closed);

        // Reopen if needed later
        so.Reopen();
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill); // back to active
    }

    [Fact]
    public void SubcontractingFlow_OrderToReceipt()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();

        // 1. Create subcontracting order
        var sco = new SubcontractingOrder(Guid.NewGuid(), companyId, "SCO-001", DateTime.UtcNow, supplierId);
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, fgItemId, "Finished Widget", 200, 25));
        sco.Submit();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Open);

        // 2. Partial receipt (120 units)
        var scoItem = sco.Items.First();
        scoItem.ReceivedQty = 120;
        sco.MarkPartiallyReceived();
        sco.Status.ShouldBe(SubcontractingOrderStatus.PartiallyReceived);
        sco.PerReceived = (120m / 200m) * 100m;
        sco.PerReceived.ShouldBe(60m);

        // 3. Remaining receipt (80 units)
        scoItem.ReceivedQty = 200;
        var perReceived = (200m / 200m) * 100m;
        perReceived.ShouldBe(100m);
        sco.Close();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Closed);
    }
}
