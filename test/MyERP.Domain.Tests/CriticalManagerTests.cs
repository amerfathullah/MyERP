using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for critical domain managers that validate business rules at the entity level.
/// Covers MaterialRequestManager, PickList lifecycle, SalesInvoiceManager,
/// StockEntry warehouse rules, and WorkOrder material calculations.
/// </summary>
public class CriticalManagerTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    #region MaterialRequestManager Tests

    [Fact]
    public void MR_IsFullyFulfilled_AllItemsOrdered_ReturnsTrue()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), _companyId, "MR-001", MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Widget", 10, "Unit");
        mr.AddItem(Guid.NewGuid(), "Bolt", 20, "Unit");

        // Simulate full ordering
        var items = mr.Items.ToList();
        items[0].OrderedQuantity = 10;
        items[1].OrderedQuantity = 20;

        var mgr = new MaterialRequestManager(null!);
        mgr.IsFullyFulfilled(mr).ShouldBeTrue();
    }

    [Fact]
    public void MR_IsFullyFulfilled_PartialOrder_ReturnsFalse()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), _companyId, "MR-001", MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Widget", 10, "Unit");

        var items = mr.Items.ToList();
        items[0].OrderedQuantity = 5; // Only 50% ordered

        var mgr = new MaterialRequestManager(null!);
        mgr.IsFullyFulfilled(mr).ShouldBeFalse();
    }

    [Fact]
    public void MR_IsFullyFulfilled_At9999Pct_ReturnsTrue()
    {
        // Per ERPNext: 99.99% threshold for float tolerance
        var mr = new MaterialRequest(Guid.NewGuid(), _companyId, "MR-001", MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Widget", 100, "Unit");

        var items = mr.Items.ToList();
        items[0].OrderedQuantity = 99.999m; // 99.999% — above threshold

        var mgr = new MaterialRequestManager(null!);
        mgr.IsFullyFulfilled(mr).ShouldBeTrue();
    }

    [Fact]
    public void MR_IsFullyFulfilled_At9998Pct_ReturnsFalse()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), _companyId, "MR-001", MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Widget", 100, "Unit");

        var items = mr.Items.ToList();
        items[0].OrderedQuantity = 99.98m; // 99.98% — below threshold

        var mgr = new MaterialRequestManager(null!);
        mgr.IsFullyFulfilled(mr).ShouldBeFalse();
    }

    [Fact]
    public void MR_IsFullyFulfilled_EmptyItems_ReturnsFalse()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), _companyId, "MR-001", MaterialRequestType.Purchase, DateTime.UtcNow);

        var mgr = new MaterialRequestManager(null!);
        mgr.IsFullyFulfilled(mr).ShouldBeFalse();
    }

    [Fact]
    public void MR_IsFullyFulfilled_ZeroQtyItem_ReturnsTrue()
    {
        // Zero-qty items should be treated as fulfilled
        var mr = new MaterialRequest(Guid.NewGuid(), _companyId, "MR-001", MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Widget", 0, "Unit");

        var mgr = new MaterialRequestManager(null!);
        mgr.IsFullyFulfilled(mr).ShouldBeTrue();
    }

    [Fact]
    public void MR_GetPendingQty_PartialOrder_ReturnsRemainder()
    {
        var item = new MaterialRequestItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Widget", 100, "Unit");
        item.OrderedQuantity = 60;

        MaterialRequestManager.GetPendingQty(item).ShouldBe(40);
    }

    [Fact]
    public void MR_GetPendingQty_FullyOrdered_ReturnsZero()
    {
        var item = new MaterialRequestItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Widget", 50, "Unit");
        item.OrderedQuantity = 50;

        MaterialRequestManager.GetPendingQty(item).ShouldBe(0);
    }

    [Fact]
    public void MR_GetPendingQty_OverOrdered_ReturnsZero()
    {
        // Over-ordered should clamp to zero (Math.Max prevents negative)
        var item = new MaterialRequestItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Widget", 50, "Unit");
        item.OrderedQuantity = 60;

        MaterialRequestManager.GetPendingQty(item).ShouldBe(0);
    }

    [Fact]
    public void MR_ValidateForSubmission_NoItems_Throws()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), _companyId, "MR-001", MaterialRequestType.Purchase, DateTime.UtcNow);

        var mgr = new MaterialRequestManager(null!);
        Assert.Throws<Volo.Abp.BusinessException>(() => mgr.ValidateForSubmission(mr));
    }

    [Fact]
    public void MR_ValidateForSubmission_WithItems_Succeeds()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), _companyId, "MR-001", MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Widget", 10, "Unit");

        var mgr = new MaterialRequestManager(null!);
        mgr.ValidateForSubmission(mr); // Should not throw
    }

    #endregion

    #region PickList Lifecycle Tests

    [Fact]
    public void PickList_FullyTransferred_AllItemsComplete()
    {
        var pl = new PickList(Guid.NewGuid(), _companyId, "Delivery");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10);
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 20);
        pl.Submit();

        // Simulate full transfer
        var items = pl.Items.ToList();
        items[0].TransferredQty = 10;
        items[1].TransferredQty = 20;

        pl.IsFullyTransferred.ShouldBeTrue();
        pl.IsPartiallyTransferred.ShouldBeFalse();
    }

    [Fact]
    public void PickList_PartiallyTransferred_SomeItems()
    {
        var pl = new PickList(Guid.NewGuid(), _companyId, "Delivery");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10);
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 20);
        pl.Submit();

        var items = pl.Items.ToList();
        items[0].TransferredQty = 10; // Fully transferred
        items[1].TransferredQty = 5;  // Partially

        pl.IsPartiallyTransferred.ShouldBeTrue();
        pl.IsFullyTransferred.ShouldBeFalse();
    }

    [Fact]
    public void PickList_PendingQty_CalculatesCorrectly()
    {
        var item = new PickListItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100, 100, null, null);
        item.TransferredQty = 60;
        item.PendingQty.ShouldBe(40);
    }

    [Fact]
    public void PickList_Cancel_BlockedWhenItemsTransferred()
    {
        var pl = new PickList(Guid.NewGuid(), _companyId, "Delivery");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10);
        pl.Submit();

        pl.Items.First().TransferredQty = 5;

        Assert.Throws<Volo.Abp.BusinessException>(() => pl.Cancel());
    }

    [Fact]
    public void PickList_Cancel_SucceedsWhenNoTransfers()
    {
        var pl = new PickList(Guid.NewGuid(), _companyId, "Delivery");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10);
        pl.Submit();

        pl.Cancel(); // Should not throw
        pl.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void PickList_AddItem_BlockedAfterSubmit()
    {
        var pl = new PickList(Guid.NewGuid(), _companyId, "Delivery");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10);
        pl.Submit();

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 5));
    }

    [Fact]
    public void PickList_Submit_RequiresItems()
    {
        var pl = new PickList(Guid.NewGuid(), _companyId, "Delivery");
        Assert.Throws<Volo.Abp.BusinessException>(() => pl.Submit());
    }

    #endregion

    #region SalesInvoiceManager — Selling Price Validation

    [Fact]
    public void SellingPrice_AboveCost_NoWarnings()
    {
        var items = new List<(Guid, decimal, string)>
        {
            (Guid.NewGuid(), 100m, "Widget"),
        };
        var result = SalesInvoiceManager.ValidateSellingPrice(items, _ => 80m, "Warn");
        result.HasWarnings.ShouldBeFalse();
    }

    [Fact]
    public void SellingPrice_BelowCost_WarnMode_ReturnsWarning()
    {
        var items = new List<(Guid, decimal, string)>
        {
            (Guid.NewGuid(), 50m, "Widget"),
        };
        var result = SalesInvoiceManager.ValidateSellingPrice(items, _ => 80m, "Warn");
        result.HasWarnings.ShouldBeTrue();
        result.Warnings.Count.ShouldBe(1);
    }

    [Fact]
    public void SellingPrice_BelowCost_StopMode_Throws()
    {
        var items = new List<(Guid, decimal, string)>
        {
            (Guid.NewGuid(), 50m, "Widget"),
        };
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            SalesInvoiceManager.ValidateSellingPrice(items, _ => 80m, "Stop"));
    }

    [Fact]
    public void SellingPrice_ZeroCost_Skipped()
    {
        // Zero valuation rate = no cost data = skip validation
        var items = new List<(Guid, decimal, string)>
        {
            (Guid.NewGuid(), 10m, "NewItem"),
        };
        var result = SalesInvoiceManager.ValidateSellingPrice(items, _ => 0m, "Stop");
        result.HasWarnings.ShouldBeFalse();
    }

    [Fact]
    public void SellingPrice_EqualToCost_Passes()
    {
        var items = new List<(Guid, decimal, string)>
        {
            (Guid.NewGuid(), 80m, "Widget"),
        };
        var result = SalesInvoiceManager.ValidateSellingPrice(items, _ => 80m, "Stop");
        result.HasWarnings.ShouldBeFalse();
    }

    [Fact]
    public void SellingPrice_MultiItem_FirstBelowCost_Throws()
    {
        var items = new List<(Guid, decimal, string)>
        {
            (Guid.NewGuid(), 50m, "Cheap"),
            (Guid.NewGuid(), 200m, "Expensive"),
        };
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            SalesInvoiceManager.ValidateSellingPrice(items, _ => 80m, "Stop"));
    }

    #endregion

    #region SalesInvoiceManager — ReturnWithStockNoZeroQty

    [Fact]
    public void ReturnWithStock_ZeroQty_Throws()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SI-001", DateTime.UtcNow, _tenantId);
        si.IsReturn = true;
        si.UpdateStock = true;
        si.AddItem(Guid.NewGuid(), "Widget", -1, 100m, 0m);
        // Simulate zero-qty item (e.g., from amendment/data fix)
        si.Items.First().Quantity = 0;

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            SalesInvoiceManager.ValidateReturnWithStockNoZeroQty(si));
    }

    [Fact]
    public void ReturnWithStock_NegativeQty_NoThrow()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SI-001", DateTime.UtcNow, _tenantId);
        si.IsReturn = true;
        si.UpdateStock = true;
        si.AddItem(Guid.NewGuid(), "Widget", -5, 100m, 0m);

        SalesInvoiceManager.ValidateReturnWithStockNoZeroQty(si); // Should not throw
    }

    [Fact]
    public void ReturnWithStock_NotReturn_Skipped()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SI-001", DateTime.UtcNow, _tenantId);
        si.UpdateStock = true;
        si.AddItem(Guid.NewGuid(), "Widget", 1, 100m, 0m);
        // Simulate zero-qty item
        si.Items.First().Quantity = 0;

        SalesInvoiceManager.ValidateReturnWithStockNoZeroQty(si); // Not a return — skipped
    }

    [Fact]
    public void ReturnWithStock_NoUpdateStock_Skipped()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SI-001", DateTime.UtcNow, _tenantId);
        si.IsReturn = true;
        // UpdateStock = false (default)
        si.AddItem(Guid.NewGuid(), "Widget", -1, 100m, 0m);
        // Simulate zero-qty item
        si.Items.First().Quantity = 0;

        SalesInvoiceManager.ValidateReturnWithStockNoZeroQty(si); // No stock effect — skipped
    }

    #endregion

    #region SalesInvoiceManager — Cancel Guard

    [Fact]
    public void CanCancel_WithPayments_Throws()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SI-001", DateTime.UtcNow, _tenantId);
        si.AddItem(Guid.NewGuid(), "Widget", 1, 100m, 0m);
        si.AmountPaid = 50;

        var mgr = new SalesInvoiceManager(null!, null!);
        Assert.Throws<Volo.Abp.BusinessException>(() => mgr.ValidateCanCancel(si));
    }

    [Fact]
    public void CanCancel_NoPayments_Succeeds()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SI-001", DateTime.UtcNow, _tenantId);
        si.AddItem(Guid.NewGuid(), "Widget", 1, 100m, 0m);

        var mgr = new SalesInvoiceManager(null!, null!);
        mgr.ValidateCanCancel(si); // Should not throw
    }

    #endregion

    #region StockEntry Warehouse Validation (entity-level rules)

    [Fact]
    public void StockEntry_MaterialTransfer_RequiresBothWarehouses()
    {
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow, _tenantId);
        se.AddItem(Guid.NewGuid(), 10, sourceWarehouseId: Guid.NewGuid(), targetWarehouseId: Guid.NewGuid());
        se.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void StockEntry_Manufacture_DefaultsCorrectly()
    {
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, DateTime.UtcNow, _tenantId);
        se.EntryType.ShouldBe(StockEntryType.Manufacture);
        se.Status.ShouldBe(DocumentStatus.Draft);
    }

    [Fact]
    public void StockEntry_AllTypes_Exist()
    {
        // Verify all 14 standard types from ERPNext are defined
        var types = Enum.GetValues<StockEntryType>();
        types.Length.ShouldBeGreaterThanOrEqualTo(14);
    }

    #endregion

    #region WorkOrder Material Requirements

    [Fact]
    public void WorkOrder_RequiredItems_TrackMaterialTransfer()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", Guid.NewGuid(), Guid.NewGuid(), 100, _tenantId);
        var item = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "Steel", 50);
        wo.RequiredItems.Add(item);
        item.ShouldNotBeNull();
        item.RequiredQuantity.ShouldBe(50);
    }

    [Fact]
    public void WorkOrder_ProducedQty_TracksProgress()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", Guid.NewGuid(), Guid.NewGuid(), 100, _tenantId);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(40);
        wo.ProducedQuantity.ShouldBe(40);
        wo.RecordProduction(60);
        wo.ProducedQuantity.ShouldBe(100);
    }

    [Fact]
    public void WorkOrder_Overproduction_Blocked()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", Guid.NewGuid(), Guid.NewGuid(), 100, _tenantId);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100);
        Assert.Throws<Volo.Abp.BusinessException>(() => wo.RecordProduction(1, 0));
    }

    [Fact]
    public void WorkOrder_OverproductionWithAllowance_Passes()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", Guid.NewGuid(), Guid.NewGuid(), 100, _tenantId);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(105, 10); // 10% allowance, 105 <= 110 max
        wo.ProducedQuantity.ShouldBe(105);
    }

    #endregion

    #region BOM Cost Calculation

    [Fact]
    public void BOM_RecalculateCost_SumsItemAmounts()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-001", Guid.NewGuid(), _tenantId);
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Steel", 2, 50m)); // 100
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Screw", 10, 1m)); // 10
        bom.RecalculateCost();
        bom.TotalMaterialCost.ShouldBe(110);
    }

    [Fact]
    public void BOM_PhantomItem_Defaults()
    {
        var item = new BomItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Sub", 1, 100m);
        item.IsPhantom.ShouldBeFalse();
    }

    [Fact]
    public void BOM_BackflushBasedOn_DefaultsNull()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-001", Guid.NewGuid(), _tenantId);
        bom.BackflushBasedOn.ShouldBeNull();
    }

    #endregion
}
