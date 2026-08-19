using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
public class StockValuationCorrectionJob : AsyncBackgroundJob<StockValuationCorrectionJobArgs>, ITransientDependency
{
    private readonly IRepository<RepostItemValuation, Guid> _repostRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly ILogger<StockValuationCorrectionJob> _logger;

    public StockValuationCorrectionJob(
        IRepository<RepostItemValuation, Guid> repostRepository,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        ILogger<StockValuationCorrectionJob> logger)
    {
        _repostRepository = repostRepository;
        _sleRepository = sleRepository;
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
                            // Inward: add inward value
                            runningValue += sle.StockValueDifference != 0
                                ? sle.StockValueDifference
                                : (sle.QuantityChange * sle.IncomingRate);
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
}

public class StockValuationCorrectionJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
}
