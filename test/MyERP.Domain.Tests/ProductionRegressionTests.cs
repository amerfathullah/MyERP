using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.HumanResources.Entities;
using MyERP.Inventory.DomainServices;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Production regression tests — complex multi-module flows validating
/// the complete business pipeline end-to-end.
/// </summary>
public class ProductionRegressionTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Guid _fiscalYearId = Guid.NewGuid();

    #region SO Fulfillment — UOM Conversion + MIN% Formula

    [Fact]
    public void SalesOrder_WithUomConversion_StockQtyCalculatedCorrectly()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", DateTime.UtcNow, _tenantId);
        so.AddItem(_itemId, "Widget", 5m, 120m, 0m, "Dozen"); // 5 Dozen @ RM120
        var item = so.Items.First();
        item.StockUom = "Unit";
        item.ConversionFactor = 12m;

        Assert.Equal(60m, item.StockQty); // 5 × 12 = 60
    }

    [Fact]
    public void SalesOrder_PerDelivered_MinFormula_AllItemsMustDeliver()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", DateTime.UtcNow, _tenantId);
        so.AddItem(Guid.NewGuid(), "Item A", 10m, 50m, 0m);
        so.AddItem(Guid.NewGuid(), "Item B", 20m, 30m, 0m);
        so.Submit();

        so.Items.First().DeliveredQty = 10m;  // 100%
        so.Items.Last().DeliveredQty = 0m;    // 0%
        so.UpdateFulfillmentStatus();

        // MIN(100%, 0%) → stays open
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);
    }

    [Fact]
    public void SalesOrder_FullyDeliveredAndBilled_Completed()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", DateTime.UtcNow, _tenantId);
        so.AddItem(_itemId, "Widget", 10m, 100m, 0m);
        so.Submit();

        so.Items.First().DeliveredQty = 10m;
        so.Items.First().BilledQty = 10m;
        so.UpdateFulfillmentStatus();

        Assert.Equal(DocumentStatus.Completed, so.Status);
    }

    [Fact]
    public void SalesOrder_FullyDelivered_ToBill()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", DateTime.UtcNow, _tenantId);
        so.AddItem(_itemId, "Widget", 10m, 100m, 0m);
        so.Submit();

        so.Items.First().DeliveredQty = 10m;
        so.UpdateFulfillmentStatus();

        Assert.Equal(DocumentStatus.ToBill, so.Status);
    }

    [Fact]
    public void SalesOrder_Close_AndReopen_RecalculatesStatus()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", DateTime.UtcNow, _tenantId);
        so.AddItem(_itemId, "Widget", 10m, 100m, 0m);
        so.Submit();
        so.Items.First().DeliveredQty = 10m;
        so.Close();
        so.Reopen();

        Assert.Equal(DocumentStatus.ToBill, so.Status); // Fully delivered, not billed
    }

    #endregion

    #region Credit Note — Outstanding Reduction

    [Fact]
    public void SalesInvoice_CreditNote_NegativeGrandTotal()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SI-001", DateTime.UtcNow, _tenantId);
        si.IsReturn = true;
        si.ReturnAgainstId = Guid.NewGuid();
        si.AddItem(_itemId, "Return Widget", -5m, 100m, 0m);

        Assert.True(si.GrandTotal < 0);
    }

    [Fact]
    public void SalesInvoice_PartialPayment_ReducesOutstanding()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SI-001", DateTime.UtcNow, _tenantId);
        si.AddItem(_itemId, "Widget", 10m, 100m, 0m);

        si.AmountPaid = 400m;
        Assert.Equal(600m, si.OutstandingAmount);
    }

    [Fact]
    public void SalesInvoice_FullPayment_ZeroOutstanding()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SI-001", DateTime.UtcNow, _tenantId);
        si.AddItem(_itemId, "Widget", 5m, 200m, 0m);

        si.AmountPaid = 1000m;
        Assert.Equal(0m, si.OutstandingAmount);
    }

    #endregion

    #region PO Lifecycle — Submit → Receipt → Billing → Complete

    [Fact]
    public void PurchaseOrder_Submit_IsToDeliverAndBill()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-001", DateTime.UtcNow, _tenantId);
        po.AddItem(_itemId, "Raw Material", 50m, 10m, 0m);
        po.Submit();

        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);
    }

    [Fact]
    public void PurchaseOrder_FullyReceivedAndBilled_Completed()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-001", DateTime.UtcNow, _tenantId);
        po.AddItem(_itemId, "Raw Material", 50m, 10m, 0m);
        po.Submit();

        po.Items.First().ReceivedQty = 50m;
        po.Items.First().BilledQty = 50m;
        po.UpdateFulfillmentStatus();

        Assert.Equal(DocumentStatus.Completed, po.Status);
    }

    [Fact]
    public void PurchaseOrder_Close_FromSubmitted()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-001", DateTime.UtcNow, _tenantId);
        po.AddItem(_itemId, "Raw Material", 50m, 10m, 0m);
        po.Submit();
        po.Close();

        Assert.Equal(DocumentStatus.Closed, po.Status);
    }

    [Fact]
    public void PurchaseOrder_CloseFromDraft_Throws()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-001", DateTime.UtcNow, _tenantId);
        po.AddItem(_itemId, "Raw Material", 50m, 10m, 0m);

        Assert.ThrowsAny<Exception>(() => po.Close());
    }

    [Fact]
    public void PurchaseOrder_Reopen_RecalculatesStatus()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-001", DateTime.UtcNow, _tenantId);
        po.AddItem(_itemId, "Raw Material", 50m, 10m, 0m);
        po.Submit();
        po.Items.First().ReceivedQty = 50m;
        po.Close();
        po.Reopen();

        Assert.Equal(DocumentStatus.ToBill, po.Status);
    }

    #endregion

    #region FIFO/LIFO Valuation

    [Fact]
    public void Fifo_ConsumesOldestFirst()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(100m, 10m); // 100 @ RM10
        fifo.AddStock(50m, 15m);  // 50 @ RM15

        var consumed = fifo.RemoveStock(120m);
        // 100×10 + 20×15 = 1300; avg rate = 1300/120 ≈ 10.83
        var totalCost = consumed.Sum(b => b.Qty * b.Rate);
        var avgRate = totalCost / 120m;
        Assert.True(avgRate > 10m && avgRate < 11m);
    }

    [Fact]
    public void Fifo_NegativeStock_CreatesNegativeBin()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(10m, 20m);
        fifo.RemoveStock(15m, 25m);

        Assert.Equal(-5m, fifo.TotalQty);
    }

    [Fact]
    public void Lifo_ConsumesNewestFirst()
    {
        var lifo = new FifoValuation(isLifo: true);
        lifo.AddStock(100m, 10m);
        lifo.AddStock(50m, 20m);

        var consumed = lifo.RemoveStock(60m);
        // LIFO: 50×20 + 10×10 = 1100; avg = 1100/60 ≈ 18.33
        var totalCost = consumed.Sum(b => b.Qty * b.Rate);
        var avgRate = totalCost / 60m;
        Assert.True(avgRate > 18m && avgRate < 19m);
    }

    [Fact]
    public void Fifo_NegativeRecovery_ResetsRate()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(5m, 20m);
        fifo.RemoveStock(10m, 20m); // -5 @ 20
        fifo.AddStock(8m, 30m); // recovery → 3 remaining

        Assert.Equal(3m, fifo.TotalQty);
    }

    #endregion

    #region Work Order — Overproduction Check

    [Fact]
    public void WorkOrder_Production_WithinLimit()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", _itemId, Guid.NewGuid(), 100m, _tenantId);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(80m, 5m); // 5% allowance, max=105

        Assert.Equal(80m, wo.ProducedQuantity);
    }

    [Fact]
    public void WorkOrder_Overproduction_Throws()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", _itemId, Guid.NewGuid(), 100m, _tenantId);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(80m, 5m);

        Assert.Throws<BusinessException>(() => wo.RecordProduction(30m, 5m)); // 110 > 105
    }

    [Fact]
    public void WorkOrder_AutoCompletes_OnFullProduction()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", _itemId, Guid.NewGuid(), 100m, _tenantId);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100m, 0m);

        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
    }

    [Fact]
    public void WorkOrder_PercentComplete()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", _itemId, Guid.NewGuid(), 200m, _tenantId);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(50m, 0m);

        Assert.Equal(25m, wo.PercentComplete);
    }

    #endregion

    #region Payment Entry — Multi-Currency + Multi-Reference

    [Fact]
    public void PaymentEntry_ExchangeGain()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Receive, DateTime.UtcNow,
            1000m, _accountId, Guid.NewGuid(), _tenantId);
        pe.ExchangeRate = 4.72m;
        pe.SourceExchangeRate = 4.50m;

        Assert.Equal(220m, pe.ExchangeGainLoss); // 1000 × (4.72 - 4.50)
    }

    [Fact]
    public void PaymentEntry_ExchangeLoss()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Receive, DateTime.UtcNow,
            1000m, _accountId, Guid.NewGuid(), _tenantId);
        pe.ExchangeRate = 4.50m;
        pe.SourceExchangeRate = 4.72m;

        Assert.Equal(-220m, pe.ExchangeGainLoss);
    }

    [Fact]
    public void PaymentEntry_SameCurrency_ZeroGainLoss()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Pay, DateTime.UtcNow,
            5000m, _accountId, Guid.NewGuid(), _tenantId);
        pe.ExchangeRate = 1m;
        pe.SourceExchangeRate = 1m;

        Assert.Equal(0m, pe.ExchangeGainLoss);
    }

    [Fact]
    public void PaymentEntry_MultiReference_UnallocatedAmount()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Receive, DateTime.UtcNow,
            10000m, _accountId, Guid.NewGuid(), _tenantId);

        pe.References.Add(new PaymentEntryReference(Guid.NewGuid(), pe.Id,
            "SalesInvoice", Guid.NewGuid(), 8000m, 6000m, 6000m));
        pe.References.Add(new PaymentEntryReference(Guid.NewGuid(), pe.Id,
            "SalesInvoice", Guid.NewGuid(), 5000m, 3000m, 3000m));

        Assert.Equal(1000m, pe.UnallocatedAmount); // 10000 - 6000 - 3000
    }

    #endregion

    #region BOM — Cost + Phantom Items

    [Fact]
    public void Bom_RecalculateCost_SumsItems()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-001", _itemId, _tenantId);
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Steel", 5m, 20m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Bolt", 10m, 2m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Paint", 0.5m, 50m));
        bom.RecalculateCost();

        Assert.Equal(145m, bom.TotalMaterialCost); // 100 + 20 + 25
    }

    [Fact]
    public void BomItem_Phantom_DefaultsFalse()
    {
        var item = new BomItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, "Component", 1m, 100m);
        Assert.False(item.IsPhantom);
    }

    [Fact]
    public void BomItem_SubBomId_DefaultsNull()
    {
        var item = new BomItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, "Leaf", 1m, 50m);
        Assert.Null(item.SubBomId);
    }

    #endregion

    #region Delivery Note — Return Flow

    [Fact]
    public void DeliveryNote_Return_HasNegativeQty()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId,
            "DN-RET-001", DateTime.UtcNow, _tenantId);
        dn.IsReturn = true;
        dn.ReturnAgainstId = Guid.NewGuid();
        dn.AddItem(_itemId, "Return Widget", -5m, 100m, 0m);

        Assert.True(dn.Items.First().Quantity < 0);
        Assert.True(dn.IsReturn);
    }

    [Fact]
    public void DeliveryNote_ReturnAgainst_SetCorrectly()
    {
        var originalId = Guid.NewGuid();
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId,
            "DN-RET-001", DateTime.UtcNow, _tenantId);
        dn.IsReturn = true;
        dn.ReturnAgainstId = originalId;

        Assert.Equal(originalId, dn.ReturnAgainstId);
    }

    #endregion
}
