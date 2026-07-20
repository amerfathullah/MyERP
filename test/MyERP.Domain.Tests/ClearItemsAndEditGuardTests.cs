using System;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for ClearItems domain methods and Draft-only edit guards
/// added to support UpdateAsync on transaction documents.
/// </summary>
public class ClearItemsAndEditGuardTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    #region SalesOrder ClearItems

    [Fact]
    public void SO_ClearItems_RemovesAllItems()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-001", DateTime.UtcNow, _tenantId);
        so.AddItem(Guid.NewGuid(), "Widget", 10, 50m, 3m);
        so.AddItem(Guid.NewGuid(), "Bolt", 20, 5m, 0.3m);
        so.Items.Count.ShouldBe(2);

        so.ClearItems();
        so.Items.Count.ShouldBe(0);
        so.NetTotal.ShouldBe(0);
        so.GrandTotal.ShouldBe(0);
    }

    [Fact]
    public void SO_ClearItems_BlockedAfterSubmit()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-001", DateTime.UtcNow, _tenantId);
        so.AddItem(Guid.NewGuid(), "Widget", 10, 50m, 3m);
        so.Submit();

        Assert.Throws<Volo.Abp.BusinessException>(() => so.ClearItems());
    }

    [Fact]
    public void SO_ClearItems_ThenReaddItems()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-001", DateTime.UtcNow, _tenantId);
        so.AddItem(Guid.NewGuid(), "OldItem", 5, 100m, 6m);
        so.Items.Count.ShouldBe(1);

        so.ClearItems();
        so.AddItem(Guid.NewGuid(), "NewItem", 3, 200m, 12m);
        so.Items.Count.ShouldBe(1);
        so.Items[0].Description.ShouldBe("NewItem");
    }

    #endregion

    #region PurchaseOrder ClearItems

    [Fact]
    public void PO_ClearItems_RemovesAllItems()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "PO-001", DateTime.UtcNow, _tenantId);
        po.AddItem(Guid.NewGuid(), "Steel", 100, 10m, 0.6m);
        po.AddItem(Guid.NewGuid(), "Bolt", 500, 0.5m, 0.03m);
        po.Items.Count.ShouldBe(2);

        po.ClearItems();
        po.Items.Count.ShouldBe(0);
        po.NetTotal.ShouldBe(0);
    }

    [Fact]
    public void PO_ClearItems_BlockedAfterSubmit()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "PO-001", DateTime.UtcNow, _tenantId);
        po.AddItem(Guid.NewGuid(), "Steel", 100, 10m, 0.6m);
        po.Submit();

        Assert.Throws<Volo.Abp.BusinessException>(() => po.ClearItems());
    }

    #endregion

    #region StockEntry ClearItems

    [Fact]
    public void SE_ClearItems_RemovesAllItems()
    {
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialReceipt, DateTime.UtcNow, _tenantId);
        se.AddItem(Guid.NewGuid(), 10, null, Guid.NewGuid());
        se.AddItem(Guid.NewGuid(), 20, null, Guid.NewGuid());
        se.Items.Count.ShouldBe(2);

        se.ClearItems();
        se.Items.Count.ShouldBe(0);
    }

    [Fact]
    public void SE_ClearItems_BlockedAfterSubmit()
    {
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialReceipt, DateTime.UtcNow, _tenantId);
        se.AddItem(Guid.NewGuid(), 10, null, Guid.NewGuid());
        se.Submit();

        Assert.Throws<Volo.Abp.BusinessException>(() => se.ClearItems());
    }

    [Fact]
    public void SE_ClearItems_ThenReaddItems()
    {
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialReceipt, DateTime.UtcNow, _tenantId);
        se.AddItem(Guid.NewGuid(), 10, null, Guid.NewGuid());
        se.ClearItems();
        se.AddItem(Guid.NewGuid(), 25, null, Guid.NewGuid());
        se.Items.Count.ShouldBe(1);
        se.Items[0].Quantity.ShouldBe(25);
    }

    #endregion

    #region Draft-only Edit Guards

    [Fact]
    public void SO_DraftCanBeEdited()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-001", DateTime.UtcNow, _tenantId);
        so.Status.ShouldBe(DocumentStatus.Draft);
        // Draft status allows editing (AddItem, ClearItems work)
        so.AddItem(Guid.NewGuid(), "Item", 1, 100, 6);
        so.ClearItems();
    }

    [Fact]
    public void PO_SubmittedCannotBeEdited()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "PO-001", DateTime.UtcNow, _tenantId);
        po.AddItem(Guid.NewGuid(), "Item", 1, 100, 6);
        po.Submit();
        // Submitted status blocks editing
        Assert.Throws<Volo.Abp.BusinessException>(() => po.AddItem(Guid.NewGuid(), "New", 1, 50, 3));
    }

    [Fact]
    public void SE_PostedCannotBeEdited()
    {
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialReceipt, DateTime.UtcNow, _tenantId);
        se.AddItem(Guid.NewGuid(), 10, null, Guid.NewGuid());
        se.Submit();
        se.Post();
        // Posted status blocks editing
        Assert.Throws<Volo.Abp.BusinessException>(() => se.ClearItems());
        Assert.Throws<Volo.Abp.BusinessException>(() => se.AddItem(Guid.NewGuid(), 5, null, Guid.NewGuid()));
    }

    #endregion

    #region Warehouse Propagation on Edit (ClearItems + Re-add)

    [Fact]
    public void SO_ClearAndReadd_PreservesWarehouseId()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-001", DateTime.UtcNow, _tenantId);
        var warehouseId = Guid.NewGuid();
        so.AddItem(Guid.NewGuid(), "Widget", 10, 50m, 3m);
        so.Items[0].WarehouseId = warehouseId;

        // Simulate UpdateAsync: clear + re-add with warehouse
        so.ClearItems();
        so.AddItem(Guid.NewGuid(), "Widget v2", 15, 55m, 3.3m);
        so.Items[^1].WarehouseId = warehouseId;

        so.Items[0].WarehouseId.ShouldBe(warehouseId);
        so.Items[0].Quantity.ShouldBe(15);
    }

    [Fact]
    public void PO_ClearAndReadd_PreservesWarehouseId()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "PO-001", DateTime.UtcNow, _tenantId);
        var warehouseId = Guid.NewGuid();
        po.AddItem(Guid.NewGuid(), "Steel", 100, 10m, 0.6m);
        po.Items[0].WarehouseId = warehouseId;

        // Simulate UpdateAsync: clear + re-add with warehouse
        po.ClearItems();
        po.AddItem(Guid.NewGuid(), "Steel v2", 200, 12m, 0.72m);
        po.Items[^1].WarehouseId = warehouseId;

        po.Items[0].WarehouseId.ShouldBe(warehouseId);
    }

    [Fact]
    public void SO_ItemWarehouseId_DefaultsNull()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-001", DateTime.UtcNow, _tenantId);
        so.AddItem(Guid.NewGuid(), "Widget", 10, 50m, 3m);
        so.Items[0].WarehouseId.ShouldBeNull();
    }

    [Fact]
    public void PO_ItemWarehouseId_DefaultsNull()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "PO-001", DateTime.UtcNow, _tenantId);
        po.AddItem(Guid.NewGuid(), "Steel", 100, 10m, 0.6m);
        po.Items[0].WarehouseId.ShouldBeNull();
    }

    [Fact]
    public void SO_MultipleItems_IndependentWarehouses()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-001", DateTime.UtcNow, _tenantId);
        var wh1 = Guid.NewGuid();
        var wh2 = Guid.NewGuid();
        so.AddItem(Guid.NewGuid(), "Item A", 5, 100m, 6m);
        so.AddItem(Guid.NewGuid(), "Item B", 3, 200m, 12m);
        so.Items[0].WarehouseId = wh1;
        so.Items[1].WarehouseId = wh2;

        so.Items[0].WarehouseId.ShouldBe(wh1);
        so.Items[1].WarehouseId.ShouldBe(wh2);
        so.Items[0].WarehouseId.ShouldNotBe(so.Items[1].WarehouseId);
    }

    #endregion
}
