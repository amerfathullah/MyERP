using System;
using System.Threading.Tasks;
using MyERP.EInvoice.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.EInvoice.Services;

/// <summary>
/// Orchestrates e-Invoice submission, status checking, and cancellation.
/// Migrated from myinvois submit_purchase.py, get_status.py, cancel_doc.py.
/// </summary>
public class EInvoiceService : DomainService
{
    private readonly ILhdnApiClient _lhdnApiClient;
    private readonly IRepository<EInvoiceSubmission, Guid> _submissionRepository;
    private readonly IRepository<LhdnSuccessLog, Guid> _successLogRepository;

    public EInvoiceService(
        ILhdnApiClient lhdnApiClient,
        IRepository<EInvoiceSubmission, Guid> submissionRepository,
        IRepository<LhdnSuccessLog, Guid> successLogRepository)
    {
        _lhdnApiClient = lhdnApiClient;
        _submissionRepository = submissionRepository;
        _successLogRepository = successLogRepository;
    }

    /// <summary>
    /// Submit an invoice document to LHDN MyInvois.
    /// Per myinvois original.py: retries once with a refreshed bearer token on HTTP 401/500.
    /// </summary>
    public async Task<EInvoiceSubmission> SubmitAsync(
        Guid companyId,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string xmlDocument,
        string accessToken,
        LhdnEnvironment environment,
        Guid? tenantId = null,
        Func<Task<string?>>? refreshTokenAsync = null)
    {
        var nextId = LazyServiceProvider != null ? GuidGenerator.Create() : Guid.NewGuid();
        var submission = new EInvoiceSubmission(
            nextId,
            companyId,
            sourceDocumentType,
            sourceDocumentId,
            tenantId);

        // Persist before the network call so a transport-layer failure (timeout, DNS,
        // connection reset) still leaves an audit trail — the row stays "Pending" instead
        // of the submission attempt vanishing with no trace.
        await _submissionRepository.InsertAsync(submission);

        var response = await _lhdnApiClient.SubmitDocumentAsync(accessToken, xmlDocument, environment);

        // Per myinvois original.py lines 408-415: retry once with refreshed bearer token on 401/500
        if (!response.IsSuccess && (response.StatusCode == 401 || response.StatusCode == 500) && refreshTokenAsync != null)
        {
            var refreshedToken = await refreshTokenAsync();
            if (!string.IsNullOrWhiteSpace(refreshedToken))
            {
                response = await _lhdnApiClient.SubmitDocumentAsync(refreshedToken, xmlDocument, environment);
            }
        }

        if (response.IsSuccess && !string.IsNullOrWhiteSpace(response.DocumentUuid))
        {
            var qrCodeUrl = response.QrCodeUrl
                ?? LhdnApiClient.BuildQrVerificationUrl(environment, response.DocumentUuid, response.LongId);

            submission.MarkAccepted(
                response.SubmissionUid!,
                response.DocumentUuid,
                response.LongId,
                qrCodeUrl,
                response.RawJson);

            var logId = LazyServiceProvider != null ? GuidGenerator.Create() : Guid.NewGuid();
            var log = new LhdnSuccessLog(
                logId,
                companyId,
                submission.Id,
                response.DocumentUuid,
                sourceDocumentType,
                sourceDocumentId,
                tenantId)
            {
                LongId = response.LongId,
                QrCodeUrl = qrCodeUrl,
                ResponseJson = response.RawJson
            };
            await _successLogRepository.InsertAsync(log);
        }
        else
        {
            submission.MarkRejected(response.ErrorMessage ?? "Unknown error", response.RawJson);
        }

        await _submissionRepository.UpdateAsync(submission);
        return submission;
    }

    /// <summary>
    /// Refresh the status of a submission from LHDN.
    /// Per myinvois get_status.py: retries once on HTTP 401/500 with a refreshed bearer token,
    /// updates QR code URL, document UUID, longId, and marks validated time when status = "Valid".
    /// </summary>
    public async Task<EInvoiceSubmission> RefreshStatusAsync(
        Guid submissionId,
        string accessToken,
        LhdnEnvironment environment,
        Func<Task<string?>>? refreshTokenAsync = null)
    {
        var submission = await _submissionRepository.GetAsync(submissionId);

        if (string.IsNullOrEmpty(submission.SubmissionUid))
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceSubmissionFailed);

        var response = await _lhdnApiClient.GetDocumentStatusAsync(
            accessToken, submission.SubmissionUid, environment);

        // Per myinvois get_status.py lines 65-70: retry once with refreshed bearer token on 401/500
        if (!response.IsSuccess && (response.StatusCode == 401 || response.StatusCode == 500) && refreshTokenAsync != null)
        {
            var refreshedToken = await refreshTokenAsync();
            if (!string.IsNullOrWhiteSpace(refreshedToken))
            {
                response = await _lhdnApiClient.GetDocumentStatusAsync(
                    refreshedToken, submission.SubmissionUid, environment);
            }
        }

        if (!response.IsSuccess)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceSubmissionFailed)
                .WithData("reason", response.ErrorMessage ?? $"Failed to retrieve LHDN status (HTTP {response.StatusCode}).");
        }

        // Update all fields from LHDN response (per myinvois get_status.py: case-insensitive status match)
        var previousStatus = submission.Status;
        var normalizedStatus = string.IsNullOrWhiteSpace(response.Status) ? "Unknown" : response.Status.Trim();
        submission.Status = normalizedStatus;
        if (response.DocumentUuid != null) submission.DocumentUuid = response.DocumentUuid;
        if (response.LongId != null) submission.LongId = response.LongId;
        if (response.QrCodeUrl != null)
        {
            submission.QrCodeUrl = response.QrCodeUrl;
        }
        else if (string.IsNullOrWhiteSpace(submission.QrCodeUrl))
        {
            submission.QrCodeUrl = LhdnApiClient.BuildQrVerificationUrl(
                environment, submission.DocumentUuid, submission.LongId);
        }

        // Per myinvois get_status.py line 92: mark validated timestamp when LHDN confirms "Valid" (case-insensitive)
        if (string.Equals(normalizedStatus, "Valid", StringComparison.OrdinalIgnoreCase) && submission.ValidatedAt == null)
            submission.ValidatedAt = DateTime.UtcNow;

        // Per myinvois: detect cancellation by LHDN (counter-party reject, case-insensitive)
        if (string.Equals(normalizedStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(previousStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            submission.CancelledAt = DateTime.UtcNow;

        if ((string.Equals(normalizedStatus, "Invalid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
            && !string.IsNullOrWhiteSpace(response.ErrorMessage))
        {
            submission.Reason = response.ErrorMessage;
        }

        await _submissionRepository.UpdateAsync(submission);
        return submission;
    }

    /// <summary>
    /// Cancel a submitted document within the 72-hour window.
    /// Mirrors myinvois cancel_doc.py logic.
    /// </summary>
    public async Task<EInvoiceSubmission> CancelAsync(
        Guid submissionId,
        string reason,
        string accessToken,
        LhdnEnvironment environment,
        Func<Task<string?>>? refreshTokenAsync = null)
    {
        var submission = await _submissionRepository.GetAsync(submissionId);

        if (string.IsNullOrEmpty(submission.SubmissionUid) || string.IsNullOrEmpty(submission.DocumentUuid))
        {
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceCancellationFailed)
                .WithData("reason", "Missing submission UID or UUID. Cannot proceed with cancellation.");
        }

        if (submission.Status == "Cancelled")
        {
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceCancellationFailed)
                .WithData("reason", "Document is already cancelled.");
        }

        if (submission.Status != "Valid"
            && submission.Status != "Submitted"
            && submission.Status != "Accepted"
            && submission.Status != "Pending")
        {
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceCancellationFailed)
                .WithData("reason", $"Cannot cancel e-Invoice with status '{submission.Status}'. Only submitted, accepted, pending, or valid documents can be cancelled.");
        }

        if (!submission.SubmittedAt.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceCancellationFailed)
                .WithData("reason", "Submission time not found. Cannot validate cancellation window.");
        }

        // Enforce 72-hour cancellation window per LHDN regulation
        var elapsed = DateTime.UtcNow - submission.SubmittedAt.Value;
        if (elapsed.TotalHours > 72)
        {
            throw new BusinessException(MyERPDomainErrorCodes.LhdnCancellationWindowExpired)
                .WithData("reason", "As per LHDN Regulation, Cancellation not allowed after 72 hours of submission.");
        }

        var response = await _lhdnApiClient.CancelDocumentAsync(
            accessToken, submission.DocumentUuid, reason, environment);

        // Per myinvois cancel_doc.py: retry once with a refreshed bearer token on 401/500
        if (!response.IsSuccess && (response.StatusCode == 401 || response.StatusCode == 500) && refreshTokenAsync != null)
        {
            var refreshedToken = await refreshTokenAsync();
            if (!string.IsNullOrWhiteSpace(refreshedToken))
            {
                response = await _lhdnApiClient.CancelDocumentAsync(
                    refreshedToken, submission.DocumentUuid, reason, environment);
            }
        }

        if (response.IsSuccess)
        {
            submission.MarkCancelled(reason);
        }
        else
        {
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceCancellationFailed)
                .WithData("reason", response.ErrorMessage ?? "Cancellation failed");
        }

        await _submissionRepository.UpdateAsync(submission);
        return submission;
    }
}
