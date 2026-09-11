using System;
using System.Linq;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for Packing Slip item name persistence (ERPNext PR #58925).
/// </summary>
public class PackingSlipItemNameTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _deliveryNoteId = Guid.NewGuid();

    [Fact]
    public void PackingSlip_AddItem_StoresItemName()
    {
        var ps = new PackingSlip(Guid.NewGuid(), _companyId, _deliveryNoteId, 1, 2);
        var itemId = Guid.NewGuid();

        ps.AddItem(itemId, 5m, 10.5m, "Custom Item Description", "Widget Pro X");

        var item = ps.Items.Single();
        Assert.Equal(itemId, item.ItemId);
        Assert.Equal(5m, item.Qty);
        Assert.Equal(10.5m, item.NetWeight);
        Assert.Equal("Custom Item Description", item.Description);
        Assert.Equal("Widget Pro X", item.ItemName);
    }

    [Fact]
    public void PackingSlipItem_Constructor_SetsItemName()
    {
        var psId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var item = new PackingSlipItem(Guid.NewGuid(), psId, itemId, 10m, 2.5m, "Test Desc", "Steel Bolt M8");

        Assert.Equal(psId, item.PackingSlipId);
        Assert.Equal(itemId, item.ItemId);
        Assert.Equal(10m, item.Qty);
        Assert.Equal(2.5m, item.NetWeight);
        Assert.Equal("Test Desc", item.Description);
        Assert.Equal("Steel Bolt M8", item.ItemName);
    }

    [Fact]
    public void PackingSlip_AddItem_DefaultItemNameIsNull()
    {
        var ps = new PackingSlip(Guid.NewGuid(), _companyId, _deliveryNoteId, 1, 1);
        var itemId = Guid.NewGuid();

        ps.AddItem(itemId, 1m, 0.5m);

        var item = ps.Items.Single();
        Assert.Null(item.ItemName);
    }
}
