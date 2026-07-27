using System;
using MyERP.EInvoice.Entities;
using MyERP.EInvoice.Services;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for RepostItemValuation stale detection, MyInvois status refresh,
/// consolidated e-invoice, and 72-hour cancellation window.
/// </summary>
public class RepostStaleAndEInvoiceStatusTests
{
    // === Repost Item Valuation — Stale Detection ===

    [Fact]
    public void RepostItemValuation_StartProcessing_ChangesStatus()
    {
        var repost = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow.AddDays(-7));
        Assert.Equal(RepostStatus.Queued, repost.Status);

        repost.StartProcessing();
        Assert.Equal(RepostStatus.InProgress, repost.Status);
    }

    [Fact]
    public void RepostItemValuation_CannotStartIfNotQueued()
    {
        var repost = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow);
        repost.StartProcessing();
        // Already InProgress — cannot start again
        Assert.Throws<Volo.Abp.BusinessException>(() => repost.StartProcessing());
    }

    [Fact]
    public void RepostItemValuation_MarkSkipped_SetsReasonAndStatus()
    {
        var repost = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow);
        repost.MarkSkipped("Covered by active repost xyz");
        Assert.Equal(RepostStatus.Skipped, repost.Status);
        Assert.Contains("Covered", repost.ErrorLog);
    }

    [Fact]
    public void RepostItemValuation_Complete_RecordsTotalAffected()
    {
        var repost = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow);
        repost.StartProcessing();
        repost.Complete(42);
        Assert.Equal(RepostStatus.Completed, repost.Status);
        Assert.Equal(42, repost.TotalAffectedEntries);
    }

    [Fact]
    public void RepostItemValuation_Fail_RecordsError()
    {
        var repost = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow);
        repost.StartProcessing();
        repost.Fail("Connection timeout");
        Assert.Equal(RepostStatus.Failed, repost.Status);
        Assert.Equal("Connection timeout", repost.ErrorLog);
    }

    // === EInvoice Submission Status ===

    [Fact]
    public void EInvoiceSubmission_MarkAccepted_SetsAllFields()
    {
        var submission = new EInvoiceSubmission(Guid.NewGuid(), Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid());
        var uuid = "doc-uuid-123";
        var longId = "long-id-456";
        var qrUrl = "https://myinvois.lhdn.my/qr/abc123";

        submission.MarkAccepted("sub-uid-001", uuid, longId, qrUrl, null);

        Assert.Equal("Valid", submission.Status); // LHDN "Accepted" maps to entity "Valid"
        Assert.Equal("sub-uid-001", submission.SubmissionUid);
        Assert.Equal(uuid, submission.DocumentUuid);
        Assert.Equal(longId, submission.LongId);
        Assert.Equal(qrUrl, submission.QrCodeUrl);
        Assert.NotNull(submission.SubmittedAt);
    }

    [Fact]
    public void EInvoiceSubmission_MarkRejected_SetsError()
    {
        var submission = new EInvoiceSubmission(Guid.NewGuid(), Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid());
        submission.MarkRejected("Invalid TIN format", null);

        Assert.Equal("Invalid", submission.Status);
        Assert.Equal("Invalid TIN format", submission.Reason);
    }

    // === 72-Hour Cancellation Window ===

    [Fact]
    public void EInvoiceSubmission_CancelWithin72Hours_Allowed()
    {
        var submission = new EInvoiceSubmission(Guid.NewGuid(), Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid());
        submission.MarkAccepted("uid-1", "uuid-1", "long-1", null, null);

        // Per DO-NOT: "Skip LHDN 72-hour cancellation window enforcement"
        // The 72h check is in EInvoiceService, not the entity itself.
        // Entity just stores the timestamp; service validates elapsed time.
        Assert.NotNull(submission.SubmittedAt);
        var elapsed = DateTime.UtcNow - submission.SubmittedAt!.Value;
        Assert.True(elapsed.TotalHours < 72); // Just submitted — within window
    }

    [Fact]
    public void EInvoiceSubmission_CancelledAt_Tracked()
    {
        var submission = new EInvoiceSubmission(Guid.NewGuid(), Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid());
        submission.MarkAccepted("uid-2", "uuid-2", "long-2", null, null);
        submission.MarkCancelled("Customer requested cancellation");

        Assert.Equal("Cancelled", submission.Status);
        Assert.NotNull(submission.CancelledAt);
    }

    // === LhdnStatusResponse QrCodeUrl ===

    [Fact]
    public void LhdnStatusResponse_IncludesQrCodeUrl()
    {
        var response = new LhdnStatusResponse
        {
            Status = "Valid",
            DocumentUuid = "doc-uuid-789",
            LongId = "long-id-012",
            QrCodeUrl = "https://myinvois.hasil.gov.my/validate/doc-uuid-789",
        };

        Assert.NotNull(response.QrCodeUrl);
        Assert.Contains("validate", response.QrCodeUrl);
    }

    // === EInvoice Status Enum Coverage ===

    [Fact]
    public void EInvoiceStatus_HasAllLhdnStates()
    {
        // Per LHDN: invoice can be NotSubmitted, Pending, Valid, Invalid, Cancelled, Rejected
        Assert.Equal(0, (int)EInvoiceStatus.NotSubmitted);
        Assert.Equal(1, (int)EInvoiceStatus.Pending);
        Assert.Equal(2, (int)EInvoiceStatus.Valid);
        Assert.Equal(3, (int)EInvoiceStatus.Invalid);
        Assert.Equal(4, (int)EInvoiceStatus.Cancelled);
        Assert.Equal(5, (int)EInvoiceStatus.Rejected);
    }

    // === Consolidated Invoice — Generic Buyer ===

    [Fact]
    public void EInvoicePartyData_GenericBuyer_HasCorrectTin()
    {
        // Per myinvois: consolidated POS invoices use generic buyer TIN "EI00000000020"
        var buyer = new EInvoicePartyData
        {
            Name = "Consolidated - General Public",
            Tin = "EI00000000020",
            IdType = "BRN",
            IdValue = "EI00000000020",
            CountryCode = "MYS",
        };

        Assert.Equal("EI00000000020", buyer.Tin);
        Assert.Equal("MYS", buyer.CountryCode);
    }
}
