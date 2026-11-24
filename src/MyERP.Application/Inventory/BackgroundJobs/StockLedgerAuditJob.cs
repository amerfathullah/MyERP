using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory.BackgroundJobs;

/// <summary>
/// Weekly Stock Ledger Audit background job per ERPNext PR #63403
/// (<c>erpnext/stock/stock_ledger_audit.py: audit_company_stock_ledger_entries</c>).
/// Audits all <c>(ItemId, WarehouseId)</c> pairs with SLEs in the current fiscal year,
/// skips pairs already queued/in-progress for stock reposting, detects the first SLE
/// violating qty/value/valuation invariants, and queues a deduplicated <see cref="RepostItemValuation"/>.
/// </summary>
public class StockLedgerAuditJob : AsyncBackgroundJob<StockLedgerAuditJobArgs>, ITransientDependency
{
    public const string SchedulerVoucherType = "StockLedgerAudit";

    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<RepostItemValuation, Guid> _repostRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly ILogger<StockLedgerAuditJob> _logger;

    public StockLedgerAuditJob(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<RepostItemValuation, Guid> repostRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        ILogger<StockLedgerAuditJob> logger)
    {
        _sleRepository = sleRepository;
        _repostRepository = repostRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(StockLedgerAuditJobArgs args)
    {
        var asOfDate = (args.AsOfDate ?? DateTime.UtcNow).Date;

        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var currentFy = fyQuery
            .Where(fy => fy.CompanyId == args.CompanyId
                && fy.StartDate <= asOfDate
                && fy.EndDate >= asOfDate)
            .OrderByDescending(fy => fy.StartDate)
            .FirstOrDefault();

        if (currentFy == null)
        {
            _logger.LogInformation(
                "StockLedgerAuditJob: No active fiscal year for company {CompanyId} on {AsOfDate}; skipping.",
                args.CompanyId, asOfDate);
            return;
        }

        var fromDateTime = currentFy.StartDate.Date;

        var sleQuery = await _sleRepository.GetQueryableAsync();
        var companySles = sleQuery
            .Where(s => s.CompanyId == args.CompanyId && !s.IsCancelled)
            .ToList();

        var pairsInWindow = companySles
            .Where(s => s.PostingDateTime >= fromDateTime)
            .Select(s => (s.ItemId, s.WarehouseId))
            .Distinct()
            .ToList();

        if (pairsInWindow.Count == 0)
            return;

        var repostQuery = await _repostRepository.GetQueryableAsync();
        var companyReposts = repostQuery
            .Where(r => r.CompanyId == args.CompanyId && r.Status != RepostStatus.Cancelled)
            .ToList();

        // Build pending stock reposts (both ItemAndWarehouse and non-accounting-only Transaction)
        var pendingSnapshots = new List<PendingRepostSnapshot>();
        foreach (var r in companyReposts)
        {
            var isQueuedOrInProgress = r.Status is RepostStatus.Queued or RepostStatus.InProgress;
            if (!isQueuedOrInProgress)
                continue;

            if (r.BasedOn == RepostMethod.ItemAndWarehouse && r.ItemId.HasValue && r.WarehouseId.HasValue)
            {
                pendingSnapshots.Add(new PendingRepostSnapshot(
                    r.ItemId.Value,
                    r.WarehouseId.Value,
                    IsSubmitted: true,
                    IsQueuedOrInProgress: true,
                    IsTransactionBased: false,
                    RepostOnlyAccountingLedgers: r.RepostOnlyAccountingLedgers));
            }
            else if (r.BasedOn == RepostMethod.Transaction && r.VoucherId.HasValue)
            {
                foreach (var pair in companySles
                    .Where(s => s.VoucherId == r.VoucherId.Value
                        && string.Equals(s.VoucherType, r.VoucherType, StringComparison.OrdinalIgnoreCase))
                    .Select(s => (s.ItemId, s.WarehouseId))
                    .Distinct())
                {
                    pendingSnapshots.Add(new PendingRepostSnapshot(
                        pair.ItemId,
                        pair.WarehouseId,
                        IsSubmitted: true,
                        IsQueuedOrInProgress: true,
                        IsTransactionBased: true,
                        RepostOnlyAccountingLedgers: r.RepostOnlyAccountingLedgers));
                }
            }
        }

        var inPendingRepost = StockLedgerAuditService.GetItemWarehousesInPendingRepost(pendingSnapshots);

        var existingSchedulerReposts = companyReposts
            .Where(r => string.Equals(r.VoucherType, SchedulerVoucherType, StringComparison.OrdinalIgnoreCase)
                && r.ItemId.HasValue
                && r.WarehouseId.HasValue)
            .Select(r => (
                r.ItemId!.Value,
                r.WarehouseId!.Value,
                r.PostingDate.Date.Add(r.PostingTime ?? TimeSpan.Zero)))
            .ToHashSet();

        foreach (var (itemId, warehouseId) in pairsInWindow)
        {
            if (inPendingRepost.Contains((itemId, warehouseId)))
                continue;

            var pairSles = companySles
                .Where(s => s.ItemId == itemId && s.WarehouseId == warehouseId)
                .OrderBy(s => s.PostingDateTime)
                .ThenBy(s => s.CreationTime)
                .ToList();

            var openingSle = pairSles
                .Where(s => s.PostingDateTime < fromDateTime)
                .LastOrDefault();

            var openingQty = openingSle?.BalanceQuantity ?? 0m;
            var openingValue = openingSle?.BalanceValue ?? 0m;

            var auditEntries = pairSles
                .Where(s => s.PostingDateTime >= fromDateTime)
                .Select(s => new StockLedgerAuditEntry(
                    EntryId: s.Id,
                    ItemId: s.ItemId,
                    WarehouseId: s.WarehouseId,
                    PostingDateTime: s.PostingDateTime,
                    ActualQty: s.QuantityChange,
                    QtyAfterTransaction: s.BalanceQuantity,
                    ValuationRate: s.ValuationRate,
                    StockValue: s.BalanceValue,
                    StockValueDifference: s.StockValueDifference,
                    IsStockReconciliation: string.Equals(s.VoucherType, "StockReconciliation", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(s.VoucherType, "Stock Reconciliation", StringComparison.OrdinalIgnoreCase),
                    ReconciliationQty: null,
                    IsAdjustmentEntry: s.IsAdjustmentEntry,
                    HasBatchNo: s.BatchId.HasValue || s.HasBatchNo,
                    UseBatchwiseValuation: false,
                    HasSerialNo: s.SerialNoId.HasValue || s.HasSerialNo,
                    HasSerialAndBatchBundle: s.SerialAndBatchBundleId.HasValue))
                .ToList();

            var firstBad = StockLedgerAuditService.FindFirstIncorrectSle(
                auditEntries,
                openingQty,
                openingValue,
                isFifoValuation: false,
                floatPrecision: args.FloatPrecision,
                currencyPrecision: args.CurrencyPrecision);

            if (!StockLedgerAuditService.ShouldCreateSchedulerRepost(firstBad, inPendingRepost, existingSchedulerReposts))
                continue;

            var repost = new RepostItemValuation(
                Guid.NewGuid(),
                args.CompanyId,
                RepostMethod.ItemAndWarehouse,
                firstBad!.PostingDateTime.Date,
                firstBad.ItemId,
                firstBad.WarehouseId,
                args.TenantId)
            {
                PostingTime = firstBad.PostingDateTime.TimeOfDay,
                VoucherType = SchedulerVoucherType,
            };

            await _repostRepository.InsertAsync(repost);
            existingSchedulerReposts.Add((firstBad.ItemId, firstBad.WarehouseId, firstBad.PostingDateTime));
        }
    }
}

public class StockLedgerAuditJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
    public int FloatPrecision { get; set; } = 3;
    public int CurrencyPrecision { get; set; } = 2;
}
