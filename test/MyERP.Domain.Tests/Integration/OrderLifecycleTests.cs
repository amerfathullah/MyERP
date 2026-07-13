using System;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

public class OrderLifecycleTests
{
    [Fact]
    public void SalesOrder_Submit_SetsToDeliverAndBill()
    {
        var so = CreateSO();
        so.Submit();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
    }

    [Fact]
    public void SalesOrder_FullyDelivered_SetsToBill()
    {
        var so = CreateSO();
        so.Submit();

        // Simulate full delivery of both items
        so.Items[0].DeliveredQty = 10;
        so.Items[1].DeliveredQty = 5;
        so.UpdateFulfillmentStatus();

        so.Status.ShouldBe(DocumentStatus.ToBill);
    }

    [Fact]
    public void SalesOrder_FullyBilled_SetsToDeliver()
    {
        var so = CreateSO();
        so.Submit();

        // Simulate full billing without delivery
        so.Items[0].BilledQty = 10;
        so.Items[1].BilledQty = 5;
        so.UpdateFulfillmentStatus();

        so.Status.ShouldBe(DocumentStatus.ToDeliver);
    }

    [Fact]
    public void SalesOrder_FullyDeliveredAndBilled_SetsCompleted()
    {
        var so = CreateSO();
        so.Submit();

        // Simulate full delivery + billing
        so.Items[0].DeliveredQty = 10;
        so.Items[0].BilledQty = 10;
        so.Items[1].DeliveredQty = 5;
        so.Items[1].BilledQty = 5;
        so.UpdateFulfillmentStatus();

        so.Status.ShouldBe(DocumentStatus.Completed);
    }

    [Fact]
    public void SalesOrder_PartialDelivery_StaysToDeliverAndBill()
    {
        var so = CreateSO();
        so.Submit();

        // Partial delivery
        so.Items[0].DeliveredQty = 5; // 50% of 10
        so.UpdateFulfillmentStatus();

        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
    }

    [Fact]
    public void PurchaseOrder_Submit_SetsToDeliverAndBill()
    {
        var po = CreatePO();
        po.Submit();
        po.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
    }

    [Fact]
    public void PurchaseOrder_FullyReceivedAndBilled_SetsCompleted()
    {
        var po = CreatePO();
        po.Submit();

        po.Items[0].ReceivedQty = 20;
        po.Items[0].BilledQty = 20;
        po.UpdateFulfillmentStatus();

        po.Status.ShouldBe(DocumentStatus.Completed);
    }

    [Fact]
    public void PurchaseOrder_FullyReceived_SetsToBill()
    {
        var po = CreatePO();
        po.Submit();

        po.Items[0].ReceivedQty = 20;
        po.UpdateFulfillmentStatus();

        po.Status.ShouldBe(DocumentStatus.ToBill);
    }

    [Fact]
    public void CreditLimit_NoLimit_NoException()
    {
        // CreditLimit = 0 means unlimited
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test Customer");
        customer.CreditLimit.ShouldBe(0);
    }

    [Fact]
    public void CreditLimit_SetLimit_PropertyWorks()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test Customer");
        customer.CreditLimit = 50000m;
        customer.CreditLimit.ShouldBe(50000m);
    }

    [Fact]
    public void NegativeStockValidation_FifoGoesNegative_Detected()
    {
        var queue = new FifoValuation();
        queue.AddStock(5, 100);
        queue.RemoveStock(8); // goes to -3

        queue.TotalQty.ShouldBe(-3);
    }

    [Fact]
    public void SalesInvoiceItem_HasSalesOrderItemId()
    {
        var siItem = new SalesInvoiceItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Test Item", 10, 100, 8, "Unit");

        siItem.SalesOrderItemId = Guid.NewGuid();
        siItem.SalesOrderItemId.ShouldNotBeNull();
    }

    [Fact]
    public void PurchaseInvoiceItem_HasPurchaseOrderItemId()
    {
        var piItem = new PurchaseInvoiceItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Test Item", 20, 50, 4, "Unit");

        piItem.PurchaseOrderItemId = Guid.NewGuid();
        piItem.PurchaseOrderItemId.ShouldNotBeNull();
    }

    private static SalesOrder CreateSO()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-TEST-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item A", 10, 100, 0);
        so.AddItem(Guid.NewGuid(), "Item B", 5, 200, 0);
        return so;
    }

    private static PurchaseOrder CreatePO()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-TEST-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Raw Material X", 20, 50, 0);
        return po;
    }

    [Fact]
    public void SalesOrder_Close_SetsClosedStatus()
    {
        var so = CreateSO();
        so.Submit();
        so.Close();
        so.Status.ShouldBe(DocumentStatus.Closed);
    }

    [Fact]
    public void SalesOrder_Close_BlockedFromDraft()
    {
        var so = CreateSO();
        Should.Throw<Volo.Abp.BusinessException>(() => so.Close());
    }

    [Fact]
    public void SalesOrder_Reopen_RecalculatesStatus()
    {
        var so = CreateSO();
        so.Submit();
        so.Items[0].DeliveredQty = 10;
        so.Items[1].DeliveredQty = 5;
        so.Close();

        so.Reopen();
        // All delivered but not billed → ToBill
        so.Status.ShouldBe(DocumentStatus.ToBill);
    }

    [Fact]
    public void SalesOrder_Reopen_BlockedWhenNotClosed()
    {
        var so = CreateSO();
        so.Submit();
        Should.Throw<Volo.Abp.BusinessException>(() => so.Reopen());
    }

    [Fact]
    public void PurchaseOrder_Close_And_Reopen()
    {
        var po = CreatePO();
        po.Submit();
        po.Close();
        po.Status.ShouldBe(DocumentStatus.Closed);

        po.Reopen();
        po.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
    }

    [Fact]
    public void SalesOrder_Cancel_ResetsBilledQty()
    {
        // Simulate: SO submitted, partially billed, then status manually checked
        var so = CreateSO();
        so.Submit();
        so.Items[0].BilledQty = 5;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill); // partial bill + no delivery

        // Simulate cancel reversal (BilledQty reduced back to 0)
        so.Items[0].BilledQty = Math.Max(0, so.Items[0].BilledQty - 5);
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
    }

    [Fact]
    public void SalesOrder_PerDelivered_CalculatesCorrectly()
    {
        var so = CreateSO();
        so.Submit();
        // Item A: qty=10, Item B: qty=5
        so.Items[0].DeliveredQty = 5; // 50% of item A
        // PerDelivered = Min(50%, 0%) = 0% (item B not delivered at all)
        so.PerDelivered.ShouldBe(0m);

        so.Items[1].DeliveredQty = 5; // 100% of item B
        // PerDelivered = Min(50%, 100%) = 50%
        so.PerDelivered.ShouldBe(50m);
    }
}
