using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Inventory.Entities;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting.BackgroundJobs;

/// <summary>
/// Processes a Repost Accounting Ledger batch: for each queued voucher, reverses its existing GL/PLE
/// via contra-entry and rebuilds fresh from current field values (see <see cref="GlRepostService"/>,
/// the single shared implementation also used by <c>GlRepostAppService</c>'s single/batch-voucher
/// admin endpoints and by <c>RepostItemValuationJob</c>'s post-valuation-repost GL fixup).
/// A retry (stuck job re-enqueued, or StartProcessing called again) leaves already-Reposted/Skipped
/// vouchers alone and only (re)processes Pending/Failed ones — mirrors ERPNext's
/// repost_accounting_ledger.repost()'s HANDLED_VOUCHER_STATUSES behavior.
/// </summary>
public class RepostAccountingLedgerJob : AsyncBackgroundJob<RepostAccountingLedgerJobArgs>, ITransientDependency
{
    private readonly IRepository<Entities.RepostAccountingLedger, Guid> _repostRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;
    private readonly GlRepostService _glService;
    private readonly ILogger<RepostAccountingLedgerJob> _logger;

    public RepostAccountingLedgerJob(
        IRepository<Entities.RepostAccountingLedger, Guid> repostRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<StockEntry, Guid> stockEntryRepository,
        GlRepostService glService,
        ILogger<RepostAccountingLedgerJob> logger)
    {
        _repostRepository = repostRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _stockEntryRepository = stockEntryRepository;
        _glService = glService;
        _logger = logger;
    }

    public override async Task ExecuteAsync(RepostAccountingLedgerJobArgs args)
    {
        var repost = (await _repostRepository.WithDetailsAsync(x => x.Vouchers))
            .FirstOrDefault(x => x.Id == args.RepostId);
        if (repost == null)
        {
            _logger.LogWarning("RepostAccountingLedgerJob: {RepostId} not found, skipping.", args.RepostId);
            return;
        }

        try
        {
            repost.StartProcessing();
            await _repostRepository.UpdateAsync(repost, autoSave: true);

            var pending = repost.Vouchers
                .Where(v => v.Status is RepostVoucherStatus.Pending or RepostVoucherStatus.Failed)
                .ToList();

            foreach (var voucher in pending)
            {
                try
                {
                    var (document, isPosted) = await LoadDocumentAsync(voucher.VoucherType, voucher.VoucherId);
                    if (document == null || !isPosted)
                    {
                        voucher.MarkSkipped("Voucher is no longer Posted (cancelled or amended since this repost was created).");
                    }
                    else
                    {
                        var result = await _glService.RepostForVoucherAsync(
                            repost.CompanyId, voucher.VoucherType, voucher.VoucherId, document);
                        if (result == null)
                        {
                            voucher.MarkSkipped("Not eligible for repost (voucher type not allowed, or posting date falls in a closed fiscal year).");
                        }
                        else
                        {
                            await SaveDocumentAsync(voucher.VoucherType, document);
                            voucher.MarkReposted();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RepostAccountingLedgerJob: failed reposting {VoucherType}/{VoucherId}",
                        voucher.VoucherType, voucher.VoucherId);
                    voucher.MarkFailed(ex.Message);
                }

                // Persist progress after every voucher so a crash mid-batch doesn't redo handled ones.
                await _repostRepository.UpdateAsync(repost, autoSave: true);
            }

            repost.Finish();
            await _repostRepository.UpdateAsync(repost, autoSave: true);

            _logger.LogInformation("RepostAccountingLedgerJob: {RepostId} finished with status {Status}.",
                repost.Id, repost.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RepostAccountingLedgerJob: {RepostId} failed.", args.RepostId);
            repost.RecordFailure(ex.Message);
            await _repostRepository.UpdateAsync(repost, autoSave: true);
        }
    }

    private async Task<(IAccountableDocument? Document, bool IsPosted)> LoadDocumentAsync(string voucherType, Guid voucherId)
    {
        switch (voucherType)
        {
            case "SalesInvoice":
                var si = await _salesInvoiceRepository.FindAsync(voucherId);
                return (si, si?.Status == DocumentStatus.Posted);
            case "PurchaseInvoice":
                var pi = await _purchaseInvoiceRepository.FindAsync(voucherId);
                return (pi, pi?.Status == DocumentStatus.Posted);
            case "StockEntry":
                var se = await _stockEntryRepository.FindAsync(voucherId);
                return (se, se?.Status == DocumentStatus.Posted);
            default:
                return (null, false);
        }
    }

    private async Task SaveDocumentAsync(string voucherType, IAccountableDocument document)
    {
        switch (voucherType)
        {
            case "SalesInvoice":
                await _salesInvoiceRepository.UpdateAsync((SalesInvoice)document, autoSave: true);
                break;
            case "PurchaseInvoice":
                await _purchaseInvoiceRepository.UpdateAsync((PurchaseInvoice)document, autoSave: true);
                break;
            case "StockEntry":
                await _stockEntryRepository.UpdateAsync((StockEntry)document, autoSave: true);
                break;
        }
    }
}

[Serializable]
public class RepostAccountingLedgerJobArgs
{
    public Guid RepostId { get; set; }
    public Guid? TenantId { get; set; }
}
