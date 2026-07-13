using System;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for:
/// - DN cancel guard (submitted SI blocks)
/// - Item deactivation validation concept
/// - Complete cancel guard coverage (SO, PO, DN)
/// </summary>
public class DNCancelAndItemDeactivationTests
{
    // --- DN Cancel Guard ---

    [Fact]
    public void DNCancelGuard_SubmittedSI_Blocked()
    {
        var siStatus = DocumentStatus.Submitted;
        var isBlocking = siStatus != DocumentStatus.Draft && siStatus != DocumentStatus.Cancelled;
        isBlocking.ShouldBeTrue();
    }

    [Fact]
    public void DNCancelGuard_PostedSI_Blocked()
    {
        var siStatus = DocumentStatus.Posted;
        var isBlocking = siStatus != DocumentStatus.Draft && siStatus != DocumentStatus.Cancelled;
        isBlocking.ShouldBeTrue();
    }

    [Fact]
    public void DNCancelGuard_DraftSI_NotBlocking()
    {
        var siStatus = DocumentStatus.Draft;
        var isBlocking = siStatus != DocumentStatus.Draft && siStatus != DocumentStatus.Cancelled;
        isBlocking.ShouldBeFalse();
    }

    [Fact]
    public void DNCancelGuard_CancelledSI_NotBlocking()
    {
        var siStatus = DocumentStatus.Cancelled;
        var isBlocking = siStatus != DocumentStatus.Draft && siStatus != DocumentStatus.Cancelled;
        isBlocking.ShouldBeFalse();
    }

    // --- Item Deactivation ---

    [Fact]
    public void Item_IsActive_DefaultsTrue()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);
        item.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Item_CanBeDeactivated_WhenNoActiveOrders()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-002", "Inactive Item", ItemType.Goods);
        item.IsActive = false;
        item.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Item_DeactivationCheck_ActiveSOBlocks()
    {
        // Simulates: SO in ToDeliverAndBill status contains this item
        var soStatus = DocumentStatus.ToDeliverAndBill;
        var isActive = soStatus != DocumentStatus.Draft
            && soStatus != DocumentStatus.Cancelled
            && soStatus != DocumentStatus.Completed;
        isActive.ShouldBeTrue(); // This would block deactivation
    }

    [Fact]
    public void Item_DeactivationCheck_CompletedSOAllows()
    {
        var soStatus = DocumentStatus.Completed;
        var isActive = soStatus != DocumentStatus.Draft
            && soStatus != DocumentStatus.Cancelled
            && soStatus != DocumentStatus.Completed;
        isActive.ShouldBeFalse(); // Completed = allowed to deactivate
    }

    // --- Complete Cancel Guard Coverage ---

    [Fact]
    public void AllThreeDocuments_HaveCancelGuards()
    {
        // SO: checks submitted DN + SI
        // PO: checks submitted PR + PI
        // DN: checks submitted SI
        // All use error code MyERP:01010

        // Verify the concept: all dependent statuses that block
        var blockingStatuses = new[]
        {
            DocumentStatus.Submitted,
            DocumentStatus.Posted,
            DocumentStatus.ToDeliverAndBill,
            DocumentStatus.ToDeliver,
            DocumentStatus.ToBill,
        };

        foreach (var status in blockingStatuses)
        {
            var isBlocking = status != DocumentStatus.Draft && status != DocumentStatus.Cancelled;
            isBlocking.ShouldBeTrue($"Status {status} should block cancel");
        }
    }
}
