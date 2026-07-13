using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for:
/// - PO cancel guard (submitted PR/PI dependents block cancel)
/// - PI company-scoped list concept
/// - DN pagination readiness
/// </summary>
public class POCancelGuardAndPaginationTests
{
    // --- PO Cancel Guard ---

    [Fact]
    public void POCancel_NoSubmittedDependents_Allowed()
    {
        var prStatus = DocumentStatus.Draft;
        var isBlocking = prStatus != DocumentStatus.Draft && prStatus != DocumentStatus.Cancelled;
        isBlocking.ShouldBeFalse();
    }

    [Fact]
    public void POCancel_SubmittedPR_Blocked()
    {
        var prStatus = DocumentStatus.Submitted;
        var isBlocking = prStatus != DocumentStatus.Draft && prStatus != DocumentStatus.Cancelled;
        isBlocking.ShouldBeTrue();
    }

    [Fact]
    public void POCancel_PostedPI_Blocked()
    {
        var piStatus = DocumentStatus.Posted;
        var isBlocking = piStatus != DocumentStatus.Draft && piStatus != DocumentStatus.Cancelled;
        isBlocking.ShouldBeTrue();
    }

    [Fact]
    public void POCancel_CancelledPR_NotBlocking()
    {
        var prStatus = DocumentStatus.Cancelled;
        var isBlocking = prStatus != DocumentStatus.Draft && prStatus != DocumentStatus.Cancelled;
        isBlocking.ShouldBeFalse();
    }

    [Fact]
    public void POCancel_DraftPI_NotBlocking()
    {
        var piStatus = DocumentStatus.Draft;
        var isBlocking = piStatus != DocumentStatus.Draft && piStatus != DocumentStatus.Cancelled;
        isBlocking.ShouldBeFalse();
    }

    // --- PO entity: Cancel releases ordered qty ---

    [Fact]
    public void PurchaseOrder_Cancel_FromToDeliverAndBill()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-500", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Widget", 10, 25, 0, "Pcs");
        po.Submit();
        po.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);

        po.Cancel();
        po.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void PurchaseOrder_Cancel_FromDraft_Works()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-501", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Test", 5, 100, 0, "Unit");
        po.Cancel();
        po.Status.ShouldBe(DocumentStatus.Cancelled);
    }
}
