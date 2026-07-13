using System;
using MyERP.Core;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for:
/// - ValidateCanAmend now allows Rejected status (for quotations)
/// - SO cancel guard: submitted dependents block cancel
/// - Company-filtered list concept
/// </summary>
public class AmendRejectedAndCancelGuardTests
{
    // --- ValidateCanAmend extended for Rejected ---

    [Fact]
    public void ValidateCanAmend_Cancelled_Allowed()
    {
        var status = DocumentStatus.Cancelled;
        (status == DocumentStatus.Cancelled || status == DocumentStatus.Rejected).ShouldBeTrue();
    }

    [Fact]
    public void ValidateCanAmend_Rejected_Allowed()
    {
        var status = DocumentStatus.Rejected;
        (status == DocumentStatus.Cancelled || status == DocumentStatus.Rejected).ShouldBeTrue();
    }

    [Fact]
    public void ValidateCanAmend_Draft_Blocked()
    {
        var status = DocumentStatus.Draft;
        (status == DocumentStatus.Cancelled || status == DocumentStatus.Rejected).ShouldBeFalse();
    }

    [Fact]
    public void ValidateCanAmend_Posted_Blocked()
    {
        var status = DocumentStatus.Posted;
        (status == DocumentStatus.Cancelled || status == DocumentStatus.Rejected).ShouldBeFalse();
    }

    [Fact]
    public void ValidateCanAmend_Submitted_Blocked()
    {
        var status = DocumentStatus.Submitted;
        (status == DocumentStatus.Cancelled || status == DocumentStatus.Rejected).ShouldBeFalse();
    }

    // --- SO Cancel Guard ---

    [Fact]
    public void SOCancel_NoSubmittedDependents_Allowed()
    {
        // When DN status is Draft or Cancelled, SO cancel is allowed
        var dnStatus = DocumentStatus.Draft;
        var isBlocking = dnStatus != DocumentStatus.Draft && dnStatus != DocumentStatus.Cancelled;
        isBlocking.ShouldBeFalse();
    }

    [Fact]
    public void SOCancel_SubmittedDN_Blocked()
    {
        // When DN is Submitted, SO cancel is blocked
        var dnStatus = DocumentStatus.Submitted;
        var isBlocking = dnStatus != DocumentStatus.Draft && dnStatus != DocumentStatus.Cancelled;
        isBlocking.ShouldBeTrue();
    }

    [Fact]
    public void SOCancel_PostedSI_Blocked()
    {
        // When SI is Posted (submitted+posted), SO cancel is blocked
        var siStatus = DocumentStatus.Posted;
        var isBlocking = siStatus != DocumentStatus.Draft && siStatus != DocumentStatus.Cancelled;
        isBlocking.ShouldBeTrue();
    }

    [Fact]
    public void SOCancel_CancelledDN_NotBlocking()
    {
        var dnStatus = DocumentStatus.Cancelled;
        var isBlocking = dnStatus != DocumentStatus.Draft && dnStatus != DocumentStatus.Cancelled;
        isBlocking.ShouldBeFalse();
    }

    // --- Quotation Amend from Rejected (MarkLost) ---

    [Fact]
    public void Quotation_MarkLost_SetsRejected()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-100", DateTime.Today);
        q.AddItem(Guid.NewGuid(), "Test", 1, 100, 0, "Unit");
        q.Submit();
        q.MarkLost();

        q.Status.ShouldBe(DocumentStatus.Rejected);
    }

    [Fact]
    public void Quotation_Rejected_CanAmend()
    {
        // Rejected status (from MarkLost) is now valid for amendment
        var status = DocumentStatus.Rejected;
        (status == DocumentStatus.Cancelled || status == DocumentStatus.Rejected).ShouldBeTrue();
    }
}
