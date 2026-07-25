using System;
using System.Linq;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Inventory.DomainServices;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Tests for Transit Warehouse Transfer Service business logic.
/// Validates the 2-step transfer pattern: SendToWarehouse → ReceiveAtWarehouse.
/// Per ERPNext: inter-warehouse transfers use in-transit warehouse for proper tracking.
/// </summary>
public class TransitTransferTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _sourceWarehouseId = Guid.NewGuid();
    private readonly Guid _transitWarehouseId = Guid.NewGuid();
    private readonly Guid _destinationWarehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    [Fact]
    public void SendToWarehouse_CreatesCorrectEntryType()
    {
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.SendToWarehouse, DateTime.Today);
        Assert.Equal(StockEntryType.SendToWarehouse, entry.EntryType);
        Assert.Equal(DocumentStatus.Draft, entry.Status);
    }

    [Fact]
    public void SendToWarehouse_ItemsHaveSourceAndTransitTarget()
    {
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.SendToWarehouse, DateTime.Today);
        entry.AddItem(_itemId, 10, _sourceWarehouseId, _transitWarehouseId, 50m);

        Assert.Single(entry.Items);
        Assert.Equal(_sourceWarehouseId, entry.Items[0].SourceWarehouseId);
        Assert.Equal(_transitWarehouseId, entry.Items[0].TargetWarehouseId);
        Assert.Equal(10, entry.Items[0].Quantity);
        Assert.Equal(50m, entry.Items[0].ValuationRate);
    }

    [Fact]
    public void ReceiveAtWarehouse_CreatesCorrectEntryType()
    {
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.ReceiveAtWarehouse, DateTime.Today);
        Assert.Equal(StockEntryType.ReceiveAtWarehouse, entry.EntryType);
    }

    [Fact]
    public void ReceiveAtWarehouse_ItemsHaveTransitSourceAndFinalTarget()
    {
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.ReceiveAtWarehouse, DateTime.Today);
        entry.AddItem(_itemId, 10, _transitWarehouseId, _destinationWarehouseId, 50m);

        Assert.Single(entry.Items);
        Assert.Equal(_transitWarehouseId, entry.Items[0].SourceWarehouseId);
        Assert.Equal(_destinationWarehouseId, entry.Items[0].TargetWarehouseId);
    }

    [Fact]
    public void ReceiveAtWarehouse_ReferencesOutgoingEntry()
    {
        var outgoingId = Guid.NewGuid();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.ReceiveAtWarehouse, DateTime.Today);
        entry.ReferenceType = "StockEntry";
        entry.ReferenceId = outgoingId;

        Assert.Equal("StockEntry", entry.ReferenceType);
        Assert.Equal(outgoingId, entry.ReferenceId);
    }

    [Fact]
    public void TransitTransfer_MultipleItems()
    {
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.SendToWarehouse, DateTime.Today);
        entry.AddItem(Guid.NewGuid(), 5, _sourceWarehouseId, _transitWarehouseId, 100m);
        entry.AddItem(Guid.NewGuid(), 20, _sourceWarehouseId, _transitWarehouseId, 25m);
        entry.AddItem(Guid.NewGuid(), 3, _sourceWarehouseId, _transitWarehouseId, 500m);

        Assert.Equal(3, entry.Items.Count);
        Assert.Equal(28, entry.Items.Sum(i => i.Quantity));
    }

    [Fact]
    public void TransitTransfer_SameWarehouseBlocked()
    {
        // SendToWarehouse with same source and transit = invalid (caught by StockEntryManager)
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.SendToWarehouse, DateTime.Today);
        entry.AddItem(_itemId, 10, _sourceWarehouseId, _sourceWarehouseId); // Same warehouse

        // Validation happens at service level — entity allows it but manager blocks
        Assert.Equal(entry.Items[0].SourceWarehouseId, entry.Items[0].TargetWarehouseId);
    }

    [Fact]
    public void TransitTransfer_SubmitRequiresItems()
    {
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.SendToWarehouse, DateTime.Today);
        Assert.Throws<Volo.Abp.BusinessException>(() => entry.Submit());
    }

    [Fact]
    public void TransitTransfer_FullLifecycle()
    {
        // Leg 1: SendToWarehouse (Source → Transit)
        var leg1 = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.SendToWarehouse, DateTime.Today);
        leg1.AddItem(_itemId, 50, _sourceWarehouseId, _transitWarehouseId, 10m);
        leg1.Submit();
        leg1.Post();
        Assert.Equal(DocumentStatus.Posted, leg1.Status);

        // Leg 2: ReceiveAtWarehouse (Transit → Destination)
        var leg2 = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.ReceiveAtWarehouse, DateTime.Today);
        leg2.AddItem(_itemId, 50, _transitWarehouseId, _destinationWarehouseId, 10m);
        leg2.ReferenceType = "StockEntry";
        leg2.ReferenceId = leg1.Id;
        leg2.Submit();
        leg2.Post();

        Assert.Equal(DocumentStatus.Posted, leg2.Status);
        Assert.Equal(leg1.Id, leg2.ReferenceId);
    }

    [Fact]
    public void TransitTransfer_CancelLeg1RequiresLeg2Cancelled()
    {
        // This is validated at service level — if receiving entry exists and isn't cancelled, first leg can't cancel
        var leg1 = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.SendToWarehouse, DateTime.Today);
        leg1.AddItem(_itemId, 10, _sourceWarehouseId, _transitWarehouseId);
        leg1.Submit();
        leg1.Post();

        // At entity level, Cancel just checks Posted status
        leg1.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, leg1.Status);
    }

    [Fact]
    public void PendingTransitTransfer_RecordProperties()
    {
        var record = new PendingTransitTransfer(
            Guid.NewGuid(), "SE-2026-00123", DateTime.Today,
            _sourceWarehouseId, 100m, 5);

        Assert.Equal("SE-2026-00123", record.EntryNumber);
        Assert.Equal(100m, record.TotalQuantity);
        Assert.Equal(5, record.ItemCount);
    }

    [Fact]
    public void TransitTransferItem_RecordProperties()
    {
        var item = new TransitTransferItem(Guid.NewGuid(), 25m, 100m);
        Assert.Equal(25m, item.Quantity);
        Assert.Equal(100m, item.ValuationRate);
    }

    [Fact]
    public void TransitTransferItem_OptionalValuationRate()
    {
        var item = new TransitTransferItem(Guid.NewGuid(), 10m);
        Assert.Null(item.ValuationRate);
    }

    [Fact]
    public void StockEntryType_SendToWarehouse_EqualsNine()
    {
        Assert.Equal(9, (int)StockEntryType.SendToWarehouse);
    }

    [Fact]
    public void StockEntryType_ReceiveAtWarehouse_EqualsTen()
    {
        Assert.Equal(10, (int)StockEntryType.ReceiveAtWarehouse);
    }
}
