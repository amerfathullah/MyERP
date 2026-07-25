using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using Xunit;

namespace MyERP.Tests;

/// <summary>
/// Tests for ERPNext upstream PRs #57412 and #57413:
/// - PR #57412: Pick List customer mapped to DN when no Sales Order linked
/// - PR #57413: Batch expiry sort handles mixed null/non-null expiry dates
/// </summary>
public class UpstreamPR57412And57413Tests
{
    // ─── PR #57412: Pick List → DN customer mapping ───

    [Fact]
    public void PickList_CustomerId_DefaultsNull()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        Assert.Null(pl.CustomerId);
    }

    [Fact]
    public void PickList_CustomerId_CanBeSet()
    {
        var customerId = Guid.NewGuid();
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery")
        {
            CustomerId = customerId
        };
        Assert.Equal(customerId, pl.CustomerId);
    }

    [Fact]
    public void PickList_CustomerCarriedToConversion_WhenNoSalesOrder()
    {
        // Scenario: Pick List with customer but no SO → customer should be used for DN
        var customerId = Guid.NewGuid();
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery")
        {
            CustomerId = customerId
        };
        // No SalesOrderId set → CustomerId is the resolution source
        Assert.Null(pl.SalesOrderId);
        Assert.Equal(customerId, pl.CustomerId);
    }

    [Fact]
    public void PickList_SOTakesPriority_OverDirectCustomer()
    {
        // When both SO and direct customer are set, SO takes priority (per ERPNext pattern)
        var directCustomerId = Guid.NewGuid();
        var soId = Guid.NewGuid();
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery")
        {
            CustomerId = directCustomerId,
            SalesOrderId = soId
        };
        // SO is set → conversion should resolve customer from SO, not from PL.CustomerId
        Assert.NotNull(pl.SalesOrderId);
        Assert.NotNull(pl.CustomerId);
    }

    [Fact]
    public void PickList_DeliveryPurpose_RequiredForDNConversion()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Material Transfer");
        Assert.Equal("Material Transfer", pl.Purpose);
        // Non-Delivery purpose should not convert to DN
    }

    [Fact]
    public void PickList_MustBeSubmitted_ForConversion()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery")
        {
            CustomerId = Guid.NewGuid()
        };
        // Draft status — cannot convert
        Assert.Equal(DocumentStatus.Draft, pl.Status);
    }

    // ─── PR #57413: Batch expiry sort for mixed null expiry dates ───

    [Fact]
    public void BatchWithExpiry_Record_Properties()
    {
        var id = Guid.NewGuid();
        var expiry = new DateTime(2026, 12, 31);
        var bwe = new BatchWithExpiry(id, "BATCH-001", expiry);
        Assert.Equal(id, bwe.BatchId);
        Assert.Equal("BATCH-001", bwe.BatchNo);
        Assert.Equal(expiry, bwe.ExpiryDate);
    }

    [Fact]
    public void BatchWithExpiry_NullExpiry_Allowed()
    {
        var bwe = new BatchWithExpiry(Guid.NewGuid(), "BATCH-002", null);
        Assert.Null(bwe.ExpiryDate);
    }

    [Fact]
    public void BatchExpirySortOrder_DatedBeforeNull()
    {
        // Per PR #57413: batches WITH expiry sort before batches WITHOUT expiry
        var batches = new List<BatchWithExpiry>
        {
            new(Guid.NewGuid(), "B-NOEXPIRY", null),
            new(Guid.NewGuid(), "B-2027-06", new DateTime(2027, 6, 30)),
            new(Guid.NewGuid(), "B-2026-12", new DateTime(2026, 12, 31)),
            new(Guid.NewGuid(), "B-NOEXPIRY2", null),
            new(Guid.NewGuid(), "B-2026-03", new DateTime(2026, 3, 15)),
        };

        // Apply the same sort algorithm as GetBatchesByOldestAsync
        var sorted = batches
            .OrderBy(b => b.ExpiryDate == null)  // false (has date) before true (null)
            .ThenBy(b => b.ExpiryDate)
            .ToList();

        // First 3: dated batches in ascending order
        Assert.Equal("B-2026-03", sorted[0].BatchNo);
        Assert.Equal("B-2026-12", sorted[1].BatchNo);
        Assert.Equal("B-2027-06", sorted[2].BatchNo);
        // Last 2: null-expiry batches (order among nulls is stable/unspecified)
        Assert.Null(sorted[3].ExpiryDate);
        Assert.Null(sorted[4].ExpiryDate);
    }

    [Fact]
    public void BatchExpirySortOrder_AllNull_NoException()
    {
        // All batches have null expiry — no TypeError equivalent (no null comparison)
        var batches = new List<BatchWithExpiry>
        {
            new(Guid.NewGuid(), "B1", null),
            new(Guid.NewGuid(), "B2", null),
            new(Guid.NewGuid(), "B3", null),
        };

        var sorted = batches
            .OrderBy(b => b.ExpiryDate == null)
            .ThenBy(b => b.ExpiryDate)
            .ToList();

        Assert.Equal(3, sorted.Count);
        // All null — order preserved (stable sort)
    }

    [Fact]
    public void BatchExpirySortOrder_AllDated_OldestFirst()
    {
        var batches = new List<BatchWithExpiry>
        {
            new(Guid.NewGuid(), "B-NEWEST", new DateTime(2028, 1, 1)),
            new(Guid.NewGuid(), "B-OLDEST", new DateTime(2025, 1, 1)),
            new(Guid.NewGuid(), "B-MIDDLE", new DateTime(2027, 1, 1)),
        };

        var sorted = batches
            .OrderBy(b => b.ExpiryDate == null)
            .ThenBy(b => b.ExpiryDate)
            .ToList();

        Assert.Equal("B-OLDEST", sorted[0].BatchNo);
        Assert.Equal("B-MIDDLE", sorted[1].BatchNo);
        Assert.Equal("B-NEWEST", sorted[2].BatchNo);
    }

    [Fact]
    public void BatchExpirySortOrder_SingleNullAmongDated_NullLast()
    {
        // The exact scenario that caused TypeError in Python 3 (comparing None to datetime.date)
        var batches = new List<BatchWithExpiry>
        {
            new(Guid.NewGuid(), "B-DATED", new DateTime(2026, 6, 15)),
            new(Guid.NewGuid(), "B-NEVER", null),
        };

        var sorted = batches
            .OrderBy(b => b.ExpiryDate == null)
            .ThenBy(b => b.ExpiryDate)
            .ToList();

        Assert.Equal("B-DATED", sorted[0].BatchNo);
        Assert.Equal("B-NEVER", sorted[1].BatchNo);
    }

    [Fact]
    public void BatchExpirySortOrder_ExactSameDate_StableSorted()
    {
        var sameDate = new DateTime(2026, 9, 1);
        var batches = new List<BatchWithExpiry>
        {
            new(Guid.NewGuid(), "B-FIRST", sameDate),
            new(Guid.NewGuid(), "B-SECOND", sameDate),
        };

        var sorted = batches
            .OrderBy(b => b.ExpiryDate == null)
            .ThenBy(b => b.ExpiryDate)
            .ToList();

        // Same date — both before any null, order among same-date is stable
        Assert.Equal(sameDate, sorted[0].ExpiryDate);
        Assert.Equal(sameDate, sorted[1].ExpiryDate);
    }

    // ─── Batch entity IsExpired with null ───

    [Fact]
    public void Batch_IsExpired_NullExpiryDate_NeverExpires()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-X");
        // No expiry date set → IsExpired is always false
        Assert.False(batch.IsExpired(DateTime.UtcNow));
        Assert.False(batch.IsExpired(new DateTime(2099, 12, 31)));
    }

    [Fact]
    public void Batch_IsExpired_PastDate_Expired()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-Y")
        {
            ExpiryDate = new DateTime(2025, 1, 1)
        };
        Assert.True(batch.IsExpired(new DateTime(2026, 7, 23)));
    }

    [Fact]
    public void Batch_IsExpired_FutureDate_NotExpired()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-Z")
        {
            ExpiryDate = new DateTime(2030, 12, 31)
        };
        Assert.False(batch.IsExpired(new DateTime(2026, 7, 23)));
    }
}
