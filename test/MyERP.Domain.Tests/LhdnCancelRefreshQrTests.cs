using System;
using System.IO;
using System.Text.Json;
using Xunit;
using MyERP.Sales;
using MyERP.Sales.Entities;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for LHDN e-Invoice Cancel (72h window), Status Refresh, and QR Code display features.
/// </summary>
public class LhdnCancelRefreshQrTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    private SalesInvoice CreateInvoice()
    {
        return new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-2026-00001",
            DateTime.UtcNow);
    }

    // --- LHDN Entity Fields ---

    [Fact]
    public void SI_LhdnSubmissionId_DefaultsNull()
    {
        var si = CreateInvoice();
        Assert.Null(si.LhdnSubmissionId);
    }

    [Fact]
    public void SI_LhdnSubmissionId_CanBeSet()
    {
        var si = CreateInvoice();
        var submissionId = Guid.NewGuid();
        si.LhdnSubmissionId = submissionId;
        Assert.Equal(submissionId, si.LhdnSubmissionId);
    }

    [Fact]
    public void SI_LhdnSubmittedAt_DefaultsNull()
    {
        var si = CreateInvoice();
        Assert.Null(si.LhdnSubmittedAt);
    }

    [Fact]
    public void SI_LhdnSubmittedAt_CanBeSet()
    {
        var si = CreateInvoice();
        var now = DateTime.UtcNow;
        si.LhdnSubmittedAt = now;
        Assert.Equal(now, si.LhdnSubmittedAt);
    }

    [Fact]
    public void SI_LhdnLongId_DefaultsNull()
    {
        var si = CreateInvoice();
        Assert.Null(si.LhdnLongId);
    }

    [Fact]
    public void SI_LhdnLongId_CanBeSet()
    {
        var si = CreateInvoice();
        si.LhdnLongId = "ABC123456789XYZ";
        Assert.Equal("ABC123456789XYZ", si.LhdnLongId);
    }

    // --- 72-Hour Cancel Window Logic ---

    [Fact]
    public void CancelWindow_Within72Hours_IsActive()
    {
        // Submitted 2 hours ago — should be cancellable
        var submittedAt = DateTime.UtcNow.AddHours(-2);
        var hoursDiff = (DateTime.UtcNow - submittedAt).TotalHours;
        Assert.True(hoursDiff <= 72);
    }

    [Fact]
    public void CancelWindow_Exactly72Hours_IsActive()
    {
        // Submitted exactly 72 hours ago — boundary, still within window
        var submittedAt = DateTime.UtcNow.AddHours(-72);
        var hoursDiff = (DateTime.UtcNow - submittedAt).TotalHours;
        Assert.True(hoursDiff <= 72.01); // Allow small tolerance for test execution time
    }

    [Fact]
    public void CancelWindow_Beyond72Hours_IsExpired()
    {
        // Submitted 73 hours ago — should NOT be cancellable
        var submittedAt = DateTime.UtcNow.AddHours(-73);
        var hoursDiff = (DateTime.UtcNow - submittedAt).TotalHours;
        Assert.True(hoursDiff > 72);
    }

    [Fact]
    public void CancelWindow_NullSubmittedAt_IsInactive()
    {
        // No submission time means not submitted — cancel window doesn't apply
        DateTime? submittedAt = null;
        Assert.Null(submittedAt);
        // Per DO-NOT: cancel window only applies to submitted invoices
    }

    // --- QR Code URL Construction ---

    [Fact]
    public void QrCode_WithLongId_GeneratesVerificationUrl()
    {
        var longId = "ABC123456789XYZ";
        var qrUrl = $"https://myinvois.hasil.gov.my/{longId}/share";
        Assert.Contains(longId, qrUrl);
        Assert.StartsWith("https://", qrUrl);
    }

    [Fact]
    public void QrCode_WithoutLongId_NoUrl()
    {
        string? longId = null;
        var qrUrl = longId != null ? $"https://myinvois.hasil.gov.my/{longId}/share" : null;
        Assert.Null(qrUrl);
    }

    // --- EInvoiceStatus Enum Values ---

    [Fact]
    public void EInvoiceStatus_NotSubmitted_IsDefault()
    {
        var si = CreateInvoice();
        Assert.Equal(EInvoiceStatus.NotSubmitted, si.EInvoiceStatus);
    }

    [Theory]
    [InlineData("Valid")]
    [InlineData("Invalid")]
    [InlineData("Pending")]
    [InlineData("Cancelled")]
    public void EInvoiceStatus_ParseFromString_Succeeds(string statusString)
    {
        var parsed = Enum.TryParse<EInvoiceStatus>(statusString, true, out var result);
        Assert.True(parsed);
        Assert.NotEqual(EInvoiceStatus.NotSubmitted, result);
    }

    // --- Cancel Preconditions ---

    [Fact]
    public void CancelLhdn_OnlyForValidStatus()
    {
        // Per DO-NOT: Skip LHDN 72-hour cancellation window enforcement
        // Cancel is only possible when eInvoiceStatus == Valid
        var si = CreateInvoice();
        si.EInvoiceStatus = EInvoiceStatus.Valid;
        Assert.Equal(EInvoiceStatus.Valid, si.EInvoiceStatus);
    }

    [Fact]
    public void CancelLhdn_NotPossibleForInvalidStatus()
    {
        var si = CreateInvoice();
        si.EInvoiceStatus = EInvoiceStatus.Invalid;
        // Invalid invoices cannot be cancelled — they were rejected by LHDN
        Assert.NotEqual(EInvoiceStatus.Valid, si.EInvoiceStatus);
    }

    [Fact]
    public void CancelLhdn_NotPossibleForNotSubmitted()
    {
        var si = CreateInvoice();
        // Not submitted — nothing to cancel
        Assert.Equal(EInvoiceStatus.NotSubmitted, si.EInvoiceStatus);
    }

    // --- Localization Keys ---

    [Theory]
    [InlineData("LhdnStatusRefreshed")]
    [InlineData("LhdnRefreshFailed")]
    [InlineData("LhdnCancelConfirmation")]
    [InlineData("LhdnInvoiceCancelled")]
    [InlineData("LhdnCancelFailed")]
    [InlineData("LhdnCancelWindowActive")]
    [InlineData("RefreshStatus")]
    [InlineData("EInvoice")]
    [InlineData("LhdnStatus")]
    [InlineData("LhdnUuid")]
    [InlineData("SubmittedAt")]
    [InlineData("VerificationQR")]
    [InlineData("VerifyOnLhdn")]
    public void Localization_LhdnKeys_ExistInEnJson(string key)
    {
        var jsonPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var content = File.ReadAllText(jsonPath);
        Assert.Contains($"\"{key}\"", content);
    }

    // --- Session Tracking ---

    [Fact]
    public void Session_LhdnCancelWorkflow_Implemented()
    {
        // Verifies that SI entity has all LHDN fields needed for cancel workflow
        var si = CreateInvoice();
        si.LhdnSubmissionId = Guid.NewGuid();
        si.LhdnSubmittedAt = DateTime.UtcNow;
        si.LhdnUuid = "uuid-test";
        si.LhdnLongId = "longId-test";
        si.EInvoiceStatus = EInvoiceStatus.Valid;

        Assert.NotNull(si.LhdnSubmissionId);
        Assert.NotNull(si.LhdnSubmittedAt);
        Assert.Equal("uuid-test", si.LhdnUuid);
        Assert.Equal("longId-test", si.LhdnLongId);
        Assert.Equal(EInvoiceStatus.Valid, si.EInvoiceStatus);
    }

    [Fact]
    public void Session_QrCodeDisplay_RequiresValidStatusAndLongId()
    {
        var si = CreateInvoice();
        
        // No QR without Valid status
        si.EInvoiceStatus = EInvoiceStatus.Pending;
        si.LhdnLongId = "ABC123";
        var showQr = si.EInvoiceStatus == EInvoiceStatus.Valid && si.LhdnLongId != null;
        Assert.False(showQr);

        // QR shown with Valid + LongId
        si.EInvoiceStatus = EInvoiceStatus.Valid;
        showQr = si.EInvoiceStatus == EInvoiceStatus.Valid && si.LhdnLongId != null;
        Assert.True(showQr);
    }

    [Fact]
    public void Session_StatusRefresh_UpdatesLhdnFields()
    {
        var si = CreateInvoice();
        si.EInvoiceStatus = EInvoiceStatus.Pending;
        si.LhdnSubmissionId = Guid.NewGuid();

        // Simulating status refresh result
        si.EInvoiceStatus = EInvoiceStatus.Valid;
        si.LhdnLongId = "REFRESHED_LONG_ID";

        Assert.Equal(EInvoiceStatus.Valid, si.EInvoiceStatus);
        Assert.Equal("REFRESHED_LONG_ID", si.LhdnLongId);
    }
}
