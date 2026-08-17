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
    /// </summary>
    public async Task<EInvoiceSubmission> SubmitAsync(
        Guid companyId,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string xmlDocument,
        string accessToken,
        LhdnEnvironment environment,
        Guid? tenantId = null)
    {
        var submission = new EInvoiceSubmission(
            GuidGenerator.Create(),
            companyId,
            sourceDocumentType,
            sourceDocumentId,
            tenantId);

        var response = await _lhdnApiClient.SubmitDocumentAsync(accessToken, xmlDocument, environment);

        if (response.IsSuccess)
        {
            submission.MarkAccepted(
                response.SubmissionUid!,
                response.DocumentUuid!,
                response.LongId,
                response.QrCodeUrl,
                response.RawJson);

            var log = new LhdnSuccessLog(
                GuidGenerator.Create(),
                companyId,
                submission.Id,
                response.DocumentUuid!,
                sourceDocumentType,
                sourceDocumentId,
                tenantId)
            {
                LongId = response.LongId,
                QrCodeUrl = response.QrCodeUrl,
                ResponseJson = response.RawJson
            };
            await _successLogRepository.InsertAsync(log);
        }
        else
        {
            submission.MarkRejected(response.ErrorMessage ?? "Unknown error", response.RawJson);
        }

        await _submissionRepository.InsertAsync(submission);
        return submission;
    }

    /// <summary>
    /// Refresh the status of a submission from LHDN.
    /// Per myinvois get_status.py: updates QR code URL, document UUID, longId,
    /// and marks validated time when status = "Valid".
    /// </summary>
    public async Task<EInvoiceSubmission> RefreshStatusAsync(
        Guid submissionId,
        string accessToken,
        LhdnEnvironment environment)
    {
        var submission = await _submissionRepository.GetAsync(submissionId);

        if (string.IsNullOrEmpty(submission.SubmissionUid))
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceSubmissionFailed);

        var response = await _lhdnApiClient.GetDocumentStatusAsync(
            accessToken, submission.SubmissionUid, environment);

        // Update all fields from LHDN response
        var previousStatus = submission.Status;
        submission.Status = response.Status;
        if (response.DocumentUuid != null) submission.DocumentUuid = response.DocumentUuid;
        if (response.LongId != null) submission.LongId = response.LongId;
        if (response.QrCodeUrl != null) submission.QrCodeUrl = response.QrCodeUrl;

        // Per myinvois: mark validated timestamp when LHDN confirms "Valid"
        if (response.Status == "Valid" && submission.ValidatedAt == null)
            submission.ValidatedAt = DateTime.UtcNow;

        // Per myinvois: detect cancellation by LHDN (counter-party reject)
        if (response.Status == "Cancelled" && previousStatus != "Cancelled")
            submission.CancelledAt = DateTime.UtcNow;

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
        LhdnEnvironment environment)
    {
        var submission = await _submissionRepository.GetAsync(submissionId);

        if (string.IsNullOrEmpty(submission.DocumentUuid))
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceCancellationFailed);

        // Enforce 72-hour cancellation window per LHDN regulation
        if (submission.SubmittedAt.HasValue)
        {
            var elapsed = DateTime.UtcNow - submission.SubmittedAt.Value;
            if (elapsed.TotalHours > 72)
            {
                throw new BusinessException(MyERPDomainErrorCodes.EInvoiceCancellationFailed)
                    .WithData("reason", "Cancellation not allowed after 72 hours of submission per LHDN regulation.");
            }
        }

        var response = await _lhdnApiClient.CancelDocumentAsync(
            accessToken, submission.DocumentUuid, reason, environment);

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
