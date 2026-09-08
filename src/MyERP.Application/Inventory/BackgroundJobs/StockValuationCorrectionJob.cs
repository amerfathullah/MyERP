using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting.DomainServices;
using MyERP.Inventory.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory.BackgroundJobs;

/// <summary>
/// Background job that processes queued Stock Valuation Repost requests.
/// Reposts future valuation rates and balances for backdated stock entries.
/// Per ERPNext: stock_reposting_settings.repost_incorrect_valuation_entries (daily scheduler).
/// </summary>
/// <remarks>
/// This is the job actually enqueued by NightlyProcessingWorker — the more complete sibling
/// implementation, RepostItemValuationJob (advisory locking, Bin sync via StockValuationService,
/// GL repost via GlRepostService), is never enqueued anywhere in the codebase and is effectively
/// dead code. Rather than swap which job runs (NightlyProcessingWorker enqueues one job per
/// company that internally loops every queued repost, while RepostItemValuationJob's args are
/// scoped to a single item+warehouse — swapping would mean restructuring the worker's enqueue
/// loop, a bigger change than closing this job's own gap), this job now reposts GL itself for
/// every voucher touched by the SLEs it corrects, matching RepostItemValuationJob's own
/// RepostAffectedGlEntriesAsync logic. Before this fix, a repost silently corrected
/// StockLedgerEntry balances/valuation while leaving GL permanently pointing at the pre-repost
/// values — invisible until someone reconciled stock value against the GL stock account.
/// </remarks>
public class StockValuationCorrectionJob : AsyncBackgroundJob<StockValuationCorrectionJobArgs>, ITransientDependency
{
    private readonly IRepository<RepostItemValuation, Guid> _repostRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly GlRepostService _glRepostService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StockValuationCorrectionJob> _logger;

    public StockValuationCorrectionJob(
        IRepository<RepostItemValuation, Guid> repostRepository,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        GlRepostService glRepostService,
        IServiceProvider serviceProvider,
        ILogger<StockValuationCorrectionJob> logger)
    {
        _repostRepository = repostRepository;
        _sleRepository = sleRepository;
        _glRepostService = glRepostService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public override async Task ExecuteAsync(StockValuationCorrectionJobArgs args)
    {
        _logger.LogInformation("StockValuationCorrectionJob: Checking queued valuation reposts for company {CompanyId}",
            args.CompanyId);

        var query = await _repostRepository.GetQueryableAsync();
        var queuedReposts = query
            .Where(r => r.CompanyId == args.CompanyId && r.Status == RepostStatus.Queued)
            .OrderBy(r => r.PostingDate)
            .ThenBy(r => r.CreationTime)
            .ToList();

        if (!queuedReposts.Any())
            return;

        var sleQuery = await _sleRepository.GetQueryableAsync();

        foreach (var repost in queuedReposts)
        {
            try
            {
                repost.StartProcessing();
                await _repostRepository.UpdateAsync(repost);

                var sles = sleQuery
                    .Where(s => s.CompanyId == args.CompanyId &&
                                s.PostingDate >= repost.PostingDate &&
                                !s.IsCancelled);

                if (repost.ItemId.HasValue)
                    sles = sles.Where(s => s.ItemId == repost.ItemId.Value);
                if (repost.WarehouseId.HasValue)
                    sles = sles.Where(s => s.WarehouseId == repost.WarehouseId.Value);

                var affectedList = sles
                    .OrderBy(s => s.PostingDateTime)
                    .ThenBy(s => s.CreationTime)
                    .ToList();

                // Group by Item + Warehouse to repost sequentially
                var grouped = affectedList.GroupBy(s => (s.ItemId, s.WarehouseId));
                var totalAffected = 0;

                foreach (var group in grouped)
                {
                    decimal runningQty = 0;
                    decimal runningValue = 0;

                    // Fetch previous balance before posting date
                    var prevSle = sleQuery
                        .Where(s => s.CompanyId == args.CompanyId &&
                                    s.ItemId == group.Key.ItemId &&
                                    s.WarehouseId == group.Key.WarehouseId &&
                                    s.PostingDate < repost.PostingDate &&
                                    !s.IsCancelled)
                        .OrderByDescending(s => s.PostingDateTime)
                        .ThenByDescending(s => s.CreationTime)
                        .FirstOrDefault();

                    if (prevSle != null)
                    {
                        runningQty = prevSle.BalanceQuantity;
                        runningValue = prevSle.BalanceValue;
                    }

                    foreach (var sle in group)
                    {
                        runningQty += sle.QuantityChange;
                        if (sle.QuantityChange > 0)
                        {
                            // Inward: add inward value at the rate this entry was originally posted
                            // at. Per StockValuationService.CreateLedgerEntryAsync (the only place
                            // that ever constructs a StockLedgerEntry), IncomingRate and
                            // StockValueDifference are never populated — they stay 0 on every real
                            // SLE — so falling back to them here silently zeroed out the recomputed
                            // value on every repost. ValuationRate is the one field that's always
                            // set to the entry's real posting rate.
                            runningValue += sle.QuantityChange * sle.ValuationRate;
                        }
                        else if (sle.QuantityChange < 0 && runningQty > 0)
                        {
                            // Outward: prorate based on previous valuation rate
                            var currentRate = (runningQty - sle.QuantityChange) > 0
                                ? (runningValue / (runningQty - sle.QuantityChange))
                                : sle.ValuationRate;
                            runningValue += sle.QuantityChange * currentRate;
                            sle.ValuationRate = currentRate;
                        }

                        sle.BalanceQuantity = runningQty;
                        sle.BalanceValue = runningValue;
                        sle.StockValue = runningValue;
                        if (runningQty > 0 && sle.QuantityChange > 0)
                        {
                            sle.ValuationRate = runningValue / runningQty;
                        }

                        await _sleRepository.UpdateAsync(sle);
                        totalAffected++;
                    }
                }

                await RepostAffectedGlEntriesAsync(repost.CompanyId, affectedList);

                repost.Complete(totalAffected);
                await _repostRepository.UpdateAsync(repost);

                _logger.LogInformation("StockValuationCorrectionJob: Completed repost {RepostId} ({Total} SLEs affected)",
                    repost.Id, totalAffected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StockValuationCorrectionJob: Failed to process repost {RepostId}", repost.Id);
                repost.Fail(ex.Message);
                await _repostRepository.UpdateAsync(repost);
            }
        }
    }

    /// <summary>
    /// Rebuilds GL entries for every stock voucher touched by the SLEs this repost corrected —
    /// otherwise GL keeps pointing at the pre-repost valuation forever. Mirrors
    /// RepostItemValuationJob.RepostAffectedGlEntriesAsync (see class remarks for why this job,
    /// not that one, needed the fix).
    /// </summary>
    private async Task RepostAffectedGlEntriesAsync(Guid companyId, List<StockLedgerEntry> affectedSles)
    {
        var affectedVouchers = affectedSles
            .Where(sle => !string.IsNullOrEmpty(sle.VoucherType) && sle.VoucherId.HasValue)
            .GroupBy(sle => new { sle.VoucherType, VoucherId = sle.VoucherId!.Value })
            .Select(g => new { g.Key.VoucherType, g.Key.VoucherId })
            .ToList();

        foreach (var voucher in affectedVouchers)
        {
            if (voucher.VoucherType == null || !GlRepostService.IsRepostAllowed(voucher.VoucherType))
                continue;

            try
            {
                var document = await LoadAccountableDocumentAsync(voucher.VoucherType, voucher.VoucherId);
                if (document == null)
                    continue;

                await _glRepostService.RepostForVoucherAsync(companyId, voucher.VoucherType, voucher.VoucherId, document);
            }
            catch (Exception ex)
            {
                // Per-voucher error isolation: one failure doesn't block the others or fail the repost.
                _logger.LogWarning(ex, "StockValuationCorrectionJob: GL repost failed for {VoucherType}/{VoucherId}",
                    voucher.VoucherType, voucher.VoucherId);
            }
        }
    }

    /// <summary>Loads a stock voucher document as IAccountableDocument for GL repost.</summary>
    private async Task<IAccountableDocument?> LoadAccountableDocumentAsync(string voucherType, Guid voucherId)
    {
        switch (voucherType)
        {
            case "StockEntry":
                var seRepo = (IRepository<MyERP.Inventory.Entities.StockEntry, Guid>)
                    _serviceProvider.GetService(typeof(IRepository<MyERP.Inventory.Entities.StockEntry, Guid>))!;
                return await seRepo.FindAsync(voucherId);

            case "PurchaseReceipt":
                var prRepo = (IRepository<MyERP.Purchasing.Entities.PurchaseReceipt, Guid>)
                    _serviceProvider.GetService(typeof(IRepository<MyERP.Purchasing.Entities.PurchaseReceipt, Guid>))!;
                return await prRepo.FindAsync(voucherId);

            case "DeliveryNote":
                var dnRepo = (IRepository<MyERP.Sales.Entities.DeliveryNote, Guid>)
                    _serviceProvider.GetService(typeof(IRepository<MyERP.Sales.Entities.DeliveryNote, Guid>))!;
                return await dnRepo.FindAsync(voucherId);

            case "SalesInvoice":
                var siRepo = (IRepository<MyERP.Sales.Entities.SalesInvoice, Guid>)
                    _serviceProvider.GetService(typeof(IRepository<MyERP.Sales.Entities.SalesInvoice, Guid>))!;
                var si = await siRepo.FindAsync(voucherId);
                return si?.UpdateStock == true ? si : null;

            case "PurchaseInvoice":
                var piRepo = (IRepository<MyERP.Purchasing.Entities.PurchaseInvoice, Guid>)
                    _serviceProvider.GetService(typeof(IRepository<MyERP.Purchasing.Entities.PurchaseInvoice, Guid>))!;
                var pi = await piRepo.FindAsync(voucherId);
                return pi?.UpdateStock == true ? pi : null;

            default:
                return null;
        }
    }
}

public class StockValuationCorrectionJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
}
