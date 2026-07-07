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

    public EInvoiceService(
        ILhdnApiClient lhdnApiClient,
        IRepository<EInvoiceSubmission, Guid> submissionRepository)
    {
        _lhdnApiClient = lhdnApiClient;
        _submissionRepository = submissionRepository;
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

        submission.Status = response.Status;
        if (response.DocumentUuid != null) submission.DocumentUuid = response.DocumentUuid;
        if (response.LongId != null) submission.LongId = response.LongId;

        if (response.Status == "Valid")
            submission.ValidatedAt = DateTime.UtcNow;

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
