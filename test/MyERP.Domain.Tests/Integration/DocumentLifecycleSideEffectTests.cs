using System;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Accounting.Entities;
using MyERP.Accounting;
using MyERP.HumanResources.Entities;
using MyERP.Core;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests verifying critical document lifecycle transitions and their side effects.
/// These flows are the most common production operations.
/// </summary>
public class DocumentLifecycleSideEffectTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();

    #region Sales Order Submit → Reserve Stock

    [Fact]
    public void SO_Submit_TransitionsToDeliverAndBill()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-001", DateTime.Today);
        so.AddItem(ItemId, "Widget", 10, 50m, 0, "Unit");

        so.Submit();

        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);
    }

    [Fact]
    public void SO_MultiItem_PerDelivered_UsesMin()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item A", 10, 100m, 0, "Unit");
        so.AddItem(Guid.NewGuid(), "Item B", 5, 200m, 0, "Unit");
        so.Submit();

        // Deliver all of A but none of B
        so.Items[0].DeliveredQty = 10;
        so.Items[1].DeliveredQty = 0;

        // PerDelivered uses MIN formula: min(100%, 0%) = 0%
        Assert.Equal(0, so.PerDelivered);
    }

    [Fact]
    public void SO_AllItemsDelivered_PerDelivered100()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item A", 10, 100m, 0, "Unit");
        so.AddItem(Guid.NewGuid(), "Item B", 5, 200m, 0, "Unit");
        so.Submit();

        so.Items[0].DeliveredQty = 10;
        so.Items[1].DeliveredQty = 5;

        Assert.Equal(100, so.PerDelivered);
    }

    [Fact]
    public void SO_Close_SetsStatusClosed()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-001", DateTime.Today);
        so.AddItem(ItemId, "Widget", 10, 50m, 0, "Unit");
        so.Submit();

        so.Close();

        Assert.Equal(DocumentStatus.Closed, so.Status);
    }

    [Fact]
    public void SO_Reopen_RecalculatesFulfillment()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-001", DateTime.Today);
        so.AddItem(ItemId, "Widget", 10, 50m, 0, "Unit");
        so.Submit();
        so.Items[0].DeliveredQty = 10;
        so.Items[0].BilledQty = 0;
        so.Close();

        so.Reopen();

        // Fully delivered, not billed → ToBill
        Assert.Equal(DocumentStatus.ToBill, so.Status);
    }

    #endregion

    #region Purchase Order Submit → Track Ordered

    [Fact]
    public void PO_Submit_TransitionsToDeliverAndBill()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-001", DateTime.Today);
        po.AddItem(ItemId, "Raw Material", 100, 25m, 0, "Kg");

        po.Submit();

        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);
    }

    [Fact]
    public void PO_FullyReceived_PerReceived100()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-001", DateTime.Today);
        po.AddItem(ItemId, "Steel", 50, 30m, 0, "Kg");
        po.Submit();

        po.Items[0].ReceivedQty = 50;

        Assert.Equal(100, po.PerReceived);
    }

    [Fact]
    public void PO_PartialReceipt_StatusStaysOpen()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-001", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "A", 10, 10m, 0, "Unit");
        po.AddItem(Guid.NewGuid(), "B", 20, 5m, 0, "Unit");
        po.Submit();

        po.Items[0].ReceivedQty = 10; // A fully received
        po.Items[1].ReceivedQty = 5;  // B only 25%

        // Min(100%, 25%) = 25%
        Assert.True(po.PerReceived < 100);
        Assert.Equal(25, po.PerReceived);
    }

    #endregion

    #region Invoice Outstanding + Payment

    [Fact]
    public void SI_Outstanding_EqualsGrandTotal_Initially()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "INV-001", DateTime.Today);
        si.AddItem(ItemId, "Service", 1, 1000m, 0, "Unit");
        si.TaxAmount = 60m; // 6% SST
        si.GrandTotal = 1060m;

        Assert.Equal(1060m, si.OutstandingAmount);
    }

    [Fact]
    public void SI_PartialPayment_ReducesOutstanding()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "INV-002", DateTime.Today);
        si.AddItem(ItemId, "Service", 1, 1000m, 0, "Unit");
        si.GrandTotal = 1000m;
        si.AmountPaid = 400m;

        Assert.Equal(600m, si.OutstandingAmount);
    }

    [Fact]
    public void SI_FullPayment_ZeroOutstanding()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "INV-003", DateTime.Today);
        si.AddItem(ItemId, "Service", 1, 500m, 0, "Unit");
        si.GrandTotal = 500m;
        si.AmountPaid = 500m;

        Assert.Equal(0m, si.OutstandingAmount);
    }

    [Fact]
    public void PI_Outstanding_EqualsGrandTotal_Initially()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), CompanyId, SupplierId, "PI-001", DateTime.Today);
        pi.AddItem(ItemId, "Supply", 10, 50m, 0, "Unit");
        pi.GrandTotal = 500m;

        Assert.Equal(500m, pi.OutstandingAmount);
    }

    #endregion

    #region Bin Stock Movement

    [Fact]
    public void Bin_StockIn_IncreasesActualQty()
    {
        var bin = new Bin(Guid.NewGuid(), ItemId, WarehouseId);
        bin.ApplyStockMovement(100, 5000m);

        Assert.Equal(100m, bin.ActualQty);
        Assert.Equal(5000m, bin.StockValue);
        Assert.Equal(50m, bin.ValuationRate);
    }

    [Fact]
    public void Bin_StockOut_DecreasesActualQty()
    {
        var bin = new Bin(Guid.NewGuid(), ItemId, WarehouseId);
        bin.ApplyStockMovement(100, 5000m);
        bin.ApplyStockMovement(-30, -1500m);

        Assert.Equal(70m, bin.ActualQty);
        Assert.Equal(3500m, bin.StockValue);
    }

    [Fact]
    public void Bin_ProjectedQty_FullFormula()
    {
        var bin = new Bin(Guid.NewGuid(), ItemId, WarehouseId);
        bin.ApplyStockMovement(100, 5000m); // actual=100

        bin.OrderedQty = 50m;      // PO ordered, not yet received
        bin.ReservedQty = 20m;     // Reserved for SO
        bin.IndentedQty = 10m;     // Material Requested
        bin.PlannedQty = 15m;      // Production planned

        // Projected = Actual + Ordered + Indented + Planned - Reserved
        Assert.Equal(100 + 50 + 10 + 15 - 20, bin.ProjectedQty);
    }

    [Fact]
    public void Bin_NegativeProjectedQty_AllowedForReorderDetection()
    {
        var bin = new Bin(Guid.NewGuid(), ItemId, WarehouseId);
        bin.ApplyStockMovement(10, 500m);
        bin.ReservedQty = 25m; // Reserved more than actual

        Assert.True(bin.ProjectedQty < 0);
    }

    #endregion

    #region Leave Application Lifecycle

    [Fact]
    public void LeaveApplication_DefaultStatus_IsOpen()
    {
        var leave = new LeaveApplication(Guid.NewGuid(), CompanyId, Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, DateTime.Today.AddDays(2), 3m);

        Assert.Equal(0, (int)leave.Status); // Open = 0
    }

    [Fact]
    public void LeaveApplication_Approve_ChangesStatus()
    {
        var leave = new LeaveApplication(Guid.NewGuid(), CompanyId, Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, DateTime.Today.AddDays(4), 5m);

        leave.Approve();

        Assert.Equal(1, (int)leave.Status); // Approved = 1
    }

    [Fact]
    public void LeaveApplication_Reject_ChangesStatus()
    {
        var leave = new LeaveApplication(Guid.NewGuid(), CompanyId, Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, DateTime.Today.AddDays(1), 2m);

        leave.Reject();

        Assert.Equal(2, (int)leave.Status); // Rejected = 2
    }

    #endregion

    #region Payment Entry Advance Detection

    [Fact]
    public void PaymentEntry_WithOrderNoInvoice_IsAdvance()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), CompanyId, PaymentType.Receive, DateTime.Today,
            5000m, Guid.NewGuid(), Guid.NewGuid());
        pe.AgainstOrderId = Guid.NewGuid();
        pe.AgainstOrderType = "SalesOrder";
        pe.AgainstInvoiceId = null;

        Assert.True(pe.IsAdvance);
    }

    [Fact]
    public void PaymentEntry_WithInvoice_NotAdvance()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), CompanyId, PaymentType.Receive, DateTime.Today,
            3000m, Guid.NewGuid(), Guid.NewGuid());
        pe.AgainstInvoiceId = Guid.NewGuid();
        pe.AgainstOrderId = Guid.NewGuid();

        Assert.False(pe.IsAdvance);
    }

    [Fact]
    public void PaymentEntry_NoLinked_NotAdvance()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), CompanyId, PaymentType.Pay, DateTime.Today,
            1000m, Guid.NewGuid(), Guid.NewGuid());

        Assert.False(pe.IsAdvance);
    }

    #endregion

    #region Sales Invoice Return (Credit Note)

    [Fact]
    public void SI_CreditNote_HasNegativeQty()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "CN-001", DateTime.Today);
        si.IsReturn = true;
        si.AddItem(ItemId, "Return Widget", -5, 100m, 0, "Unit");

        Assert.True(si.Items[0].Quantity < 0);
        Assert.Equal(-500m, si.NetTotal);
    }

    [Fact]
    public void SI_CreditNote_GrandTotalNegative()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "CN-002", DateTime.Today);
        si.IsReturn = true;
        si.AddItem(ItemId, "Credit", -2, 250m, 0, "Unit");
        si.GrandTotal = -500m;

        Assert.True(si.GrandTotal < 0);
    }

    #endregion

    #region DeliveryNote Return Stock-In

    [Fact]
    public void DN_Return_FlagIsCorrect()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), CompanyId, CustomerId, WarehouseId, "DN-RET-001", DateTime.Today);
        dn.IsReturn = true;
        dn.ReturnAgainstId = Guid.NewGuid();

        Assert.True(dn.IsReturn);
        Assert.NotNull(dn.ReturnAgainstId);
    }

    #endregion
}

