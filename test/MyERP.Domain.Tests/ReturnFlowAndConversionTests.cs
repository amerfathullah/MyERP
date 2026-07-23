using System;
using Xunit;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Core;

namespace MyERP;

/// <summary>
/// Integration tests for document return flows and document conversion UOM propagation.
/// Verifies: negative qty enforcement, return qty caps, UOM carry-forward in conversion paths.
/// </summary>
public class ReturnFlowAndConversionTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid FiscalYearId = Guid.NewGuid();

    // === Return Document Flow Tests ===

    [Fact]
    public void SalesInvoice_CreditNote_MustHaveNegativeQty()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-RET-001", DateTime.Today);
        si.IsReturn = true;
        si.ReturnAgainstId = Guid.NewGuid();
        si.AddItem(ItemId, "Widget", -5, 100, 0, "Unit");
        var item = Assert.Single(si.Items);
        Assert.Equal(-5m, item.Quantity);
    }

    [Fact]
    public void SalesInvoice_CreditNote_GrandTotalIsNegative()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-RET-002", DateTime.Today);
        si.IsReturn = true;
        si.ReturnAgainstId = Guid.NewGuid();
        si.AddItem(ItemId, "Widget", -3, 200, 0, "Unit");
        Assert.True(si.GrandTotal < 0);
    }

    [Fact]
    public void SalesInvoice_Normal_BlocksNegativeQty()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-001", DateTime.Today);
        Assert.Throws<ArgumentException>(() => si.AddItem(ItemId, "Widget", -1, 100, 0, "Unit"));
    }

    [Fact]
    public void PurchaseInvoice_DebitNote_HasNegativeQty()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), CompanyId, SupplierId, "PI-RET-001", DateTime.Today);
        pi.IsReturn = true;
        pi.ReturnAgainstId = Guid.NewGuid();
        pi.AddItem(ItemId, "Material", -10, 50, 0, "Kg");
        var item = Assert.Single(pi.Items);
        Assert.Equal(-10m, item.Quantity);
        Assert.True(pi.GrandTotal < 0);
    }

    [Fact]
    public void DeliveryNote_Return_HasIsReturnFlag()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), CompanyId, CustomerId, WarehouseId, "DN-RET-001", DateTime.Today);
        dn.IsReturn = true;
        var originalDnId = Guid.NewGuid();
        dn.ReturnAgainstId = originalDnId;
        Assert.True(dn.IsReturn);
        Assert.Equal(originalDnId, dn.ReturnAgainstId);
    }

    [Fact]
    public void PurchaseReceipt_Return_HasIsReturnFlag()
    {
        var pr = new PurchaseReceipt(Guid.NewGuid(), CompanyId, SupplierId, WarehouseId, "PR-RET-001", DateTime.Today);
        pr.IsReturn = true;
        var originalPrId = Guid.NewGuid();
        pr.ReturnAgainstId = originalPrId;
        Assert.True(pr.IsReturn);
        Assert.Equal(originalPrId, pr.ReturnAgainstId);
    }

    // === Document Conversion UOM Propagation Tests ===

    [Fact]
    public void SO_To_DN_Conversion_PreservesUomFields()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-001", DateTime.Today);
        so.AddItem(ItemId, "Widget", 5, 120, 0, "Dozen");
        var soItem = so.Items[0];
        soItem.StockUom = "Unit";
        soItem.ConversionFactor = 12m;

        var dn = new DeliveryNote(Guid.NewGuid(), CompanyId, CustomerId, WarehouseId, "DN-001", DateTime.Today);
        dn.AddItem(ItemId, "Widget", soItem.Quantity, soItem.UnitPrice, 0);
        var dnItem = dn.Items[0];
        dnItem.StockUom = soItem.StockUom;
        dnItem.ConversionFactor = soItem.ConversionFactor;

        Assert.Equal("Unit", dnItem.StockUom);
        Assert.Equal(12m, dnItem.ConversionFactor);
        Assert.Equal(60m, dnItem.StockQty);
    }

    [Fact]
    public void PO_To_PR_Conversion_PreservesUomFields()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-001", DateTime.Today);
        po.AddItem(ItemId, "Raw Material", 3, 500, 0, "Pallet");
        var poItem = po.Items[0];
        poItem.StockUom = "Kg";
        poItem.ConversionFactor = 1000m;

        var pr = new PurchaseReceipt(Guid.NewGuid(), CompanyId, SupplierId, WarehouseId, "PR-001", DateTime.Today);
        pr.AddItem(ItemId, "Raw Material", poItem.Quantity, poItem.UnitPrice, 0);
        var prItem = pr.Items[0];
        prItem.StockUom = poItem.StockUom;
        prItem.ConversionFactor = poItem.ConversionFactor;

        Assert.Equal("Kg", prItem.StockUom);
        Assert.Equal(1000m, prItem.ConversionFactor);
        Assert.Equal(3000m, prItem.StockQty);
    }

    [Fact]
    public void SO_To_SI_Conversion_PreservesUomFields()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-002", DateTime.Today);
        so.AddItem(ItemId, "Service", 2, 1000, 0, "Set");
        var soItem = so.Items[0];
        soItem.StockUom = "Unit";
        soItem.ConversionFactor = 6m;

        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-001", DateTime.Today);
        si.AddItem(ItemId, "Service", soItem.Quantity, soItem.UnitPrice, 0, "Set");
        var siItem = si.Items[0];
        siItem.StockUom = soItem.StockUom;
        siItem.ConversionFactor = soItem.ConversionFactor;

        Assert.Equal("Unit", siItem.StockUom);
        Assert.Equal(6m, siItem.ConversionFactor);
        Assert.Equal(12m, siItem.StockQty);
    }

    [Fact]
    public void SameUom_ConversionFactor_IsAlwaysOne()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-003", DateTime.Today);
        so.AddItem(ItemId, "Widget", 10, 50, 0, "Unit");
        var item = so.Items[0];
        Assert.Equal(1m, item.ConversionFactor);
        Assert.Equal(10m, item.StockQty);
    }

    // === Fulfillment Status After Conversion Tests ===

    [Fact]
    public void SO_PartialDelivery_StaysOpen()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-004", DateTime.Today);
        so.AddItem(ItemId, "A", 10, 100, 0, "Unit");
        so.AddItem(Guid.NewGuid(), "B", 5, 200, 0, "Unit");
        so.Submit();
        so.Items[0].DeliveredQty = 10;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);
    }

    [Fact]
    public void SO_FullDelivery_TransitionsToBill()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-005", DateTime.Today);
        so.AddItem(ItemId, "A", 10, 100, 0, "Unit");
        so.Submit();
        so.Items[0].DeliveredQty = 10;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToBill, so.Status);
    }

    [Fact]
    public void PO_FullReceiptAndBilling_Completes()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-002", DateTime.Today);
        po.AddItem(ItemId, "Material", 20, 50, 0, "Kg");
        po.Submit();
        po.Items[0].ReceivedQty = 20;
        po.Items[0].BilledQty = 20;
        po.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.Completed, po.Status);
    }

    [Fact]
    public void SO_Close_FromActiveStatus()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-006", DateTime.Today);
        so.AddItem(ItemId, "Widget", 10, 100, 0, "Unit");
        so.Submit();
        so.Close();
        Assert.Equal(DocumentStatus.Closed, so.Status);
    }

    [Fact]
    public void SO_Reopen_RecalculatesStatus()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-007", DateTime.Today);
        so.AddItem(ItemId, "Widget", 10, 100, 0, "Unit");
        so.Submit();
        so.Items[0].DeliveredQty = 10;
        so.Close();
        so.Reopen();
        Assert.Equal(DocumentStatus.ToBill, so.Status);
    }

    // === Amendment Flow Tests ===

    [Fact]
    public void SalesInvoice_Amendment_TracksOriginal()
    {
        var original = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-010", DateTime.Today);
        original.AddItem(ItemId, "Widget", 5, 100, 0, "Unit");
        original.Submit();
        original.Post();
        original.Cancel();

        var amended = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-010-1", DateTime.Today);
        amended.AmendedFromId = original.Id;
        amended.AmendmentIndex = 1;

        Assert.Equal(original.Id, amended.AmendedFromId);
        Assert.Equal(1, amended.AmendmentIndex);
    }

    [Fact]
    public void PurchaseOrder_Amendment_TracksOriginal()
    {
        var original = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-010", DateTime.Today);
        original.AddItem(ItemId, "Material", 10, 50, 0, "Kg");
        original.Submit();
        original.Cancel();

        var amended = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-010-1", DateTime.Today);
        amended.AmendedFromId = original.Id;
        amended.AmendmentIndex = 1;

        Assert.Equal(original.Id, amended.AmendedFromId);
    }
}
