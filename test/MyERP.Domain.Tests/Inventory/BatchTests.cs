using System;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Inventory;

public class BatchTests
{
    [Fact]
    public void IsExpired_WithFutureExpiry_ReturnsFalse()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-001");
        batch.ExpiryDate = DateTime.UtcNow.AddDays(30);

        batch.IsExpired().ShouldBeFalse();
    }

    [Fact]
    public void IsExpired_WithPastExpiry_ReturnsTrue()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-002");
        batch.ExpiryDate = DateTime.UtcNow.AddDays(-1);

        batch.IsExpired().ShouldBeTrue();
    }

    [Fact]
    public void IsExpired_WithNoExpiry_ReturnsFalse()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-003");

        batch.IsExpired().ShouldBeFalse();
    }

    [Fact]
    public void SetExpiryFromShelfLife_CalculatesCorrectly()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-004");
        batch.ManufacturingDate = new DateTime(2026, 1, 1);
        batch.ShelfLifeInDays = 365;

        batch.SetExpiryFromShelfLife();

        batch.ExpiryDate.ShouldBe(new DateTime(2027, 1, 1));
    }

    [Fact]
    public void SetExpiryFromShelfLife_WithoutManufacturingDate_DoesNothing()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-005");
        batch.ShelfLifeInDays = 90;

        batch.SetExpiryFromShelfLife();

        batch.ExpiryDate.ShouldBeNull();
    }

    [Fact]
    public void IsExpired_OnSameDayAsExpiryDate_ReturnsFalse_OnlyExpiredAfterDayPassed()
    {
        // Per ERPNext PR #58736 (commit 00f04fc084):
        // Show Expired status only AFTER expiry date has passed (< 0 days diff).
        var today = new DateTime(2026, 9, 4, 15, 30, 0, DateTimeKind.Utc);
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-TODAY")
        {
            ExpiryDate = new DateTime(2026, 9, 4)
        };

        // Same day (e.g. afternoon of expiry date) -> not yet expired
        batch.IsExpired(today).ShouldBeFalse();

        // Next day -> expired
        batch.IsExpired(today.AddDays(1)).ShouldBeTrue();
    }
}

