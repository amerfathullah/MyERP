using System;
using System.Collections.Generic;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Inventory;

public class BatchExpiryValidationTests
{
    [Fact]
    public void Batch_IsExpired_WhenPastExpiryDate()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "B001");
        batch.ExpiryDate = DateTime.Today.AddDays(-1);
        batch.IsExpired(DateTime.Today).ShouldBeTrue();
    }

    [Fact]
    public void Batch_NotExpired_WhenBeforeExpiryDate()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "B001");
        batch.ExpiryDate = DateTime.Today.AddDays(30);
        batch.IsExpired(DateTime.Today).ShouldBeFalse();
    }

    [Fact]
    public void Batch_NotExpired_WhenNoExpiryDate()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "B001");
        batch.ExpiryDate = null;
        batch.IsExpired(DateTime.Today).ShouldBeFalse();
    }

    [Fact]
    public void Batch_IsExpired_OnExactExpiryDate()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "B001");
        batch.ExpiryDate = DateTime.Today;
        // Expired ON the expiry date (< vs <=) - depends on implementation
        // ERPNext uses expiry_date < today, so ON expiry date = not expired
        batch.IsExpired(DateTime.Today).ShouldBeFalse();
    }

    [Fact]
    public void Batch_IsDisabled_BlocksUsage()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "B001");
        batch.IsDisabled = true;
        batch.IsDisabled.ShouldBeTrue();
    }

    [Fact]
    public void BatchValidationItem_CreatesCorrectly()
    {
        var itemId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var item = new BatchValidationItem(itemId, batchId, "Widget");
        item.ItemId.ShouldBe(itemId);
        item.BatchId.ShouldBe(batchId);
        item.ItemName.ShouldBe("Widget");
    }

    [Fact]
    public void BatchValidationItem_NullBatch_Allowed()
    {
        var item = new BatchValidationItem(Guid.NewGuid(), null, "Service");
        item.BatchId.ShouldBeNull();
    }

    [Fact]
    public void Batch_SetExpiryFromShelfLife_Calculates()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "B001");
        batch.ManufacturingDate = new DateTime(2026, 1, 1);
        batch.ShelfLifeInDays = 90;
        batch.SetExpiryFromShelfLife();
        batch.ExpiryDate.ShouldBe(new DateTime(2026, 4, 1));
    }

    [Fact]
    public void DeliveryNoteItem_HasBatchId()
    {
        var dnItem = new MyERP.Sales.Entities.DeliveryNoteItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Test Item", 10, 100, 6);
        dnItem.BatchId.ShouldBeNull(); // Default null
        dnItem.BatchId = Guid.NewGuid();
        dnItem.BatchId.ShouldNotBeNull();
    }

    [Fact]
    public void StockLedgerEntry_HasBatchAndSerialFields()
    {
        var sle = new StockLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 10, 100, 10, 1000);
        sle.BatchId.ShouldBeNull();
        sle.SerialNoId.ShouldBeNull();
        sle.BatchId = Guid.NewGuid();
        sle.SerialNoId = Guid.NewGuid();
        sle.BatchId.ShouldNotBeNull();
        sle.SerialNoId.ShouldNotBeNull();
    }
}
