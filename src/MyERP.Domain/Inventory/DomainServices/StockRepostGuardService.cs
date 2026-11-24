using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Stock Repost Guard — validates stock document cancellation against active Repost Item Valuation jobs.
/// Per ERPNext validate_cancellation() (gotcha #6183 &amp; commit 80bedf1791):
/// - If voucher has no Stock Ledger Entries (e.g. non-stock service receipt or update_stock=false):
///   do not block cancellation on active reposts, and auto-skip any queued repost.
/// - If Repost is InProgress: blocks cancellation (data corruption guard).
/// - If Repost is Queued: auto-skips and cancels the queued repost to allow cancellation to proceed.
/// </summary>
public class StockRepostGuardService : DomainService
{
    private readonly IRepository<RepostItemValuation, Guid> _repostRepository;
    private readonly IRepository<StockLedgerEntry, Guid>? _sleRepository;

    public StockRepostGuardService(
        IRepository<RepostItemValuation, Guid> repostRepository,
        IRepository<StockLedgerEntry, Guid>? sleRepository = null)
    {
        _repostRepository = repostRepository;
        _sleRepository = sleRepository;
    }

    /// <summary>
    /// Validates cancellation of a stock-affecting voucher.
    /// Throws BusinessException if an active repost is InProgress and the voucher posted SLEs.
    /// Auto-skips any Queued repost for this voucher so cancellation can proceed cleanly.
    /// </summary>
    public async Task ValidateCanCancelVoucherAsync(string voucherType, Guid voucherId)
    {
        var sleRepo = _sleRepository ?? LazyServiceProvider?.LazyGetService<IRepository<StockLedgerEntry, Guid>>();
        var hasSle = true;
        if (sleRepo != null)
        {
            var sleQuery = await sleRepo.GetQueryableAsync();
            hasSle = sleQuery.Any(s => s.VoucherType == voucherType && s.VoucherId == voucherId);
        }

        var query = await _repostRepository.GetQueryableAsync();
        var matchingReposts = query
            .Where(r => r.VoucherType == voucherType && r.VoucherId == voucherId)
            .ToList();

        foreach (var repost in matchingReposts)
        {
            // Per ERPNext commit 80bedf1791: skip voucher repost block on cancel when no SLEs exist
            if (!hasSle)
            {
                if (repost.Status == RepostStatus.Queued)
                {
                    repost.MarkSkipped($"Auto-skipped on cancellation of non-SLE voucher {voucherType} ({voucherId})");
                    await _repostRepository.UpdateAsync(repost, autoSave: true);
                }
                continue;
            }

            if (repost.Status == RepostStatus.InProgress)
            {
                throw new BusinessException(MyERPDomainErrorCodes.RepostAlreadyInProgress)
                    .WithData("voucherType", voucherType)
                    .WithData("voucherId", voucherId)
                    .WithData("detail", $"Cannot cancel {voucherType} while an active valuation repost is in progress.");
            }

            if (repost.Status == RepostStatus.Queued)
            {
                repost.MarkSkipped($"Auto-skipped on cancellation of source voucher {voucherType} ({voucherId})");
                await _repostRepository.UpdateAsync(repost, autoSave: true);
            }
        }
    }

    /// <summary>
    /// Determines whether a candidate Stock Ledger Entry occurs strictly later than a voucher in
    /// <c>(PostingDate, CreationTime)</c> order per ERPNext PR #68732 (<c>future_sle_exists</c>):
    /// same-timestamp entries created BEFORE the voucher's first SLE are NOT later and do not require reposting.
    /// </summary>
    public static bool IsLaterStockLedgerEntry(
        DateTime candidatePostingDate,
        DateTime candidateCreationTime,
        DateTime voucherPostingDate,
        DateTime? voucherFirstSleCreationTime)
    {
        if (candidatePostingDate > voucherPostingDate)
            return true;

        if (candidatePostingDate == voucherPostingDate
            && voucherFirstSleCreationTime.HasValue
            && candidateCreationTime > voucherFirstSleCreationTime.Value)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether any active (non-cancelled) future Stock Ledger Entry exists for another voucher
    /// on the given <paramref name="itemWarehouses"/> after <paramref name="postingDate"/> (tie-broken by
    /// the voucher's earliest SLE <c>CreationTime</c>, including cancelled SLEs).
    /// Per ERPNext PR #68732 (<c>stock_controller.py: future_sle_exists</c>).
    /// </summary>
    public async Task<bool> HasFutureStockLedgerEntriesAsync(
        string voucherType,
        Guid voucherId,
        DateTime postingDate,
        System.Collections.Generic.IEnumerable<(Guid ItemId, Guid WarehouseId)> itemWarehouses)
    {
        var sleRepo = _sleRepository ?? LazyServiceProvider?.LazyGetService<IRepository<StockLedgerEntry, Guid>>();
        if (sleRepo == null)
            return false;

        var targets = itemWarehouses?.ToHashSet();
        if (targets == null || targets.Count == 0)
            return false;

        var sleQuery = await sleRepo.GetQueryableAsync();
        var voucherFirstCreation = sleQuery
            .Where(s => s.VoucherType == voucherType && s.VoucherId == voucherId)
            .OrderBy(s => s.CreationTime)
            .Select(s => (DateTime?)s.CreationTime)
            .FirstOrDefault();

        var candidates = sleQuery
            .Where(s => !s.IsCancelled
                && s.VoucherId != voucherId
                && s.PostingDate >= postingDate)
            .ToList();

        return candidates.Any(s =>
            targets.Contains((s.ItemId, s.WarehouseId))
            && IsLaterStockLedgerEntry(s.PostingDate, s.CreationTime, postingDate, voucherFirstCreation));
    }
}
