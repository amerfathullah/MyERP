using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.EInvoice.Entities;
using MyERP.EInvoice.Services;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

namespace MyERP.EInvoice.BackgroundJobs;

/// <summary>
/// Background job that polls LHDN MyInvois API to synchronize status on pending e-Invoice submissions.
/// Per LHDN MyInvois integration (myinvois get_status.py): updates validated timestamp, QR code URL,
/// longId, and propagates the refreshed status back to the source SalesInvoice / PurchaseInvoice.
/// </summary>
public class LhdnPendingStatusSyncJob : AsyncBackgroundJob<LhdnPendingStatusSyncJobArgs>, ITransientDependency
{
    private readonly IRepository<EInvoiceSubmission, Guid> _submissionRepository;
    private readonly EInvoiceService _eInvoiceService;
    private readonly ILhdnApiClient _lhdnApiClient;
    private readonly ISettingProvider _settingProvider;
    private readonly ILogger<LhdnPendingStatusSyncJob> _logger;
    private readonly IRepository<SalesInvoice, Guid>? _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid>? _purchaseInvoiceRepository;

    public LhdnPendingStatusSyncJob(
        IRepository<EInvoiceSubmission, Guid> submissionRepository,
        EInvoiceService eInvoiceService,
        ILhdnApiClient lhdnApiClient,
        ISettingProvider settingProvider,
        ILogger<LhdnPendingStatusSyncJob> logger)
        : this(submissionRepository, eInvoiceService, lhdnApiClient, settingProvider, logger, null, null)
    {
    }

    public LhdnPendingStatusSyncJob(
        IRepository<EInvoiceSubmission, Guid> submissionRepository,
        EInvoiceService eInvoiceService,
        ILhdnApiClient lhdnApiClient,
        ISettingProvider settingProvider,
        ILogger<LhdnPendingStatusSyncJob> logger,
        IRepository<SalesInvoice, Guid>? salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid>? purchaseInvoiceRepository)
    {
        _submissionRepository = submissionRepository;
        _eInvoiceService = eInvoiceService;
        _lhdnApiClient = lhdnApiClient;
        _settingProvider = settingProvider;
        _logger = logger;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
    }

    public override async Task ExecuteAsync(LhdnPendingStatusSyncJobArgs args)
    {
        _logger.LogInformation("LhdnPendingStatusSyncJob: Checking pending LHDN submissions for company {CompanyId}",
            args.CompanyId);

        var query = await _submissionRepository.GetQueryableAsync();
        var pendingSubmissions = query
            .Where(s => s.CompanyId == args.CompanyId &&
                        (s.Status == "Pending" || s.Status == "Submitted" || s.Status == "Accepted") &&
                        !string.IsNullOrEmpty(s.SubmissionUid))
            .ToList();

        if (!pendingSubmissions.Any())
            return;

        var clientId = await _settingProvider.GetOrNullAsync("EInvoice.ClientId");
        var clientSecret = await _settingProvider.GetOrNullAsync("EInvoice.ClientSecret");
        var envString = await _settingProvider.GetOrNullAsync("EInvoice.Environment") ?? "Sandbox";

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            _logger.LogDebug("LhdnPendingStatusSyncJob: LHDN credentials not configured for company {CompanyId}. Skipping sync.",
                args.CompanyId);
            return;
        }

        var environment = TaxpayerValidationService.ParseEnvironment(envString);

        string accessToken;
        try
        {
            accessToken = await _lhdnApiClient.GetAccessTokenAsync(clientId, clientSecret, environment);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LhdnPendingStatusSyncJob: Failed to authenticate with LHDN for company {CompanyId}", args.CompanyId);
            return;
        }

        var syncedCount = 0;
        foreach (var sub in pendingSubmissions)
        {
            try
            {
                var updated = await _eInvoiceService.RefreshStatusAsync(
                    sub.Id,
                    accessToken,
                    environment,
                    refreshTokenAsync: async () =>
                    {
                        accessToken = await _lhdnApiClient.GetAccessTokenAsync(clientId, clientSecret, environment);
                        return accessToken;
                    });
                await PropagateStatusToSourceAsync(updated);
                syncedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LhdnPendingStatusSyncJob: Failed to refresh submission {SubmissionId} (UID: {Uid})",
                    sub.Id, sub.SubmissionUid);
            }
        }

        _logger.LogInformation("LhdnPendingStatusSyncJob: Synced {Count} of {Total} pending LHDN submissions for company {CompanyId}",
            syncedCount, pendingSubmissions.Count, args.CompanyId);
    }

    private async Task PropagateStatusToSourceAsync(EInvoiceSubmission submission)
    {
        if (submission.SourceDocumentType == "SalesInvoice" && _salesInvoiceRepository != null)
        {
            var invoice = await _salesInvoiceRepository.FindAsync(submission.SourceDocumentId);
            if (invoice == null) return;

            invoice.EInvoiceStatus = submission.Status switch
            {
                "Valid" => Sales.EInvoiceStatus.Valid,
                "Cancelled" => Sales.EInvoiceStatus.Cancelled,
                "Rejected" => Sales.EInvoiceStatus.Rejected,
                "Invalid" => Sales.EInvoiceStatus.Invalid,
                _ => invoice.EInvoiceStatus,
            };
            if (!string.IsNullOrWhiteSpace(submission.DocumentUuid))
                invoice.LhdnUuid = submission.DocumentUuid;
            if (!string.IsNullOrWhiteSpace(submission.LongId))
                invoice.LhdnLongId = submission.LongId;
            if (!string.IsNullOrWhiteSpace(submission.QrCodeUrl))
                invoice.QrCodeUrl = submission.QrCodeUrl;
            await _salesInvoiceRepository.UpdateAsync(invoice);
        }
        else if (submission.SourceDocumentType == "PurchaseInvoice" && _purchaseInvoiceRepository != null)
        {
            var invoice = await _purchaseInvoiceRepository.FindAsync(submission.SourceDocumentId);
            if (invoice == null) return;

            invoice.EInvoiceStatus = submission.Status switch
            {
                "Valid" => Sales.EInvoiceStatus.Valid,
                "Cancelled" => Sales.EInvoiceStatus.Cancelled,
                "Rejected" => Sales.EInvoiceStatus.Rejected,
                "Invalid" => Sales.EInvoiceStatus.Invalid,
                _ => invoice.EInvoiceStatus,
            };
            if (!string.IsNullOrWhiteSpace(submission.DocumentUuid))
                invoice.LhdnUuid = submission.DocumentUuid;
            if (!string.IsNullOrWhiteSpace(submission.LongId))
                invoice.LhdnLongId = submission.LongId;
            if (!string.IsNullOrWhiteSpace(submission.QrCodeUrl))
                invoice.QrCodeUrl = submission.QrCodeUrl;
            await _purchaseInvoiceRepository.UpdateAsync(invoice);
        }
    }
}

public class LhdnPendingStatusSyncJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
}
