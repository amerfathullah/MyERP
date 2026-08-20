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
/// Per ERPNext validate_cancellation() (gotcha #6183):
/// - If Repost is InProgress: blocks cancellation (data corruption guard).
/// - If Repost is Queued: auto-skips and cancels the queued repost to allow cancellation to proceed.
/// </summary>
public class StockRepostGuardService : DomainService
{
    private readonly IRepository<RepostItemValuation, Guid> _repostRepository;

    public StockRepostGuardService(IRepository<RepostItemValuation, Guid> repostRepository)
    {
        _repostRepository = repostRepository;
    }

    /// <summary>
    /// Validates cancellation of a stock-affecting voucher.
    /// Throws BusinessException if an active repost is InProgress.
    /// Auto-skips any Queued repost for this voucher so cancellation can proceed cleanly.
    /// </summary>
    public async Task ValidateCanCancelVoucherAsync(string voucherType, Guid voucherId)
    {
        var query = await _repostRepository.GetQueryableAsync();
        var matchingReposts = query
            .Where(r => r.VoucherType == voucherType && r.VoucherId == voucherId)
            .ToList();

        foreach (var repost in matchingReposts)
        {
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
}
