using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MyERP.Accounting.Entities;
using MyERP.Inventory.BackgroundJobs;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Application tests for <see cref="StockLedgerAuditJob"/> (ERPNext PR #63403).
/// </summary>
public class StockLedgerAuditJobTests
{
    [Fact]
    public async Task ExecuteAsync_QueuesDeduplicatedRepostForCorruptedSle_AndSkipsPendingStockReposts()
    {
        var companyId = Guid.NewGuid();
        var itemCorrupt = Guid.NewGuid();
        var itemPending = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var fyStart = new DateTime(2026, 1, 1);
        var fyEnd = new DateTime(2026, 12, 31);

        var fy = new FiscalYear(Guid.NewGuid(), companyId, "FY2026", fyStart, fyEnd);

        // Opening SLE in prior year: qty 10, val 100
        var openingSle = new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemCorrupt, whId,
            new DateTime(2025, 12, 31), quantityChange: 10m, valuationRate: 10m,
            balanceQuantity: 10m, balanceValue: 100m)
        {
            PostingDateTime = new DateTime(2025, 12, 31, 12, 0, 0),
            StockValueDifference = 100m,
        };

        // Corrupted SLE in current year: qty +5 -> balance 15, val diff +50 -> expected 150, stored 135
        var badSle = new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemCorrupt, whId,
            new DateTime(2026, 5, 10), quantityChange: 5m, valuationRate: 10m,
            balanceQuantity: 15m, balanceValue: 135m)
        {
            PostingDateTime = new DateTime(2026, 5, 10, 14, 30, 0),
            StockValueDifference = 50m,
        };

        // Corrupted SLE for itemPending which already has a queued stock repost -> must be skipped
        var pendingBadSle = new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemPending, whId,
            new DateTime(2026, 6, 1), quantityChange: 2m, valuationRate: 10m,
            balanceQuantity: 99m, balanceValue: 20m)
        {
            PostingDateTime = new DateTime(2026, 6, 1, 9, 0, 0),
            StockValueDifference = 20m,
        };

        var existingPendingRepost = new RepostItemValuation(
            Guid.NewGuid(), companyId, RepostMethod.ItemAndWarehouse,
            new DateTime(2026, 6, 1), itemPending, whId);

        var sles = new List<StockLedgerEntry> { openingSle, badSle, pendingBadSle };
        var reposts = new List<RepostItemValuation> { existingPendingRepost };
        var insertedReposts = new List<RepostItemValuation>();

        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        sleRepo.GetQueryableAsync().Returns(_ => Task.FromResult(sles.AsQueryable()));

        var repostRepo = Substitute.For<IRepository<RepostItemValuation, Guid>>();
        repostRepo.GetQueryableAsync().Returns(_ => Task.FromResult(reposts.AsQueryable()));
        repostRepo.InsertAsync(Arg.Any<RepostItemValuation>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var entity = call.Arg<RepostItemValuation>();
                reposts.Add(entity);
                insertedReposts.Add(entity);
                return Task.FromResult(entity);
            });

        var fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
        fyRepo.GetQueryableAsync().Returns(_ => Task.FromResult(new[] { fy }.AsQueryable()));

        var job = new StockLedgerAuditJob(sleRepo, repostRepo, fyRepo, NullLogger<StockLedgerAuditJob>.Instance);
        var args = new StockLedgerAuditJobArgs { CompanyId = companyId, AsOfDate = new DateTime(2026, 11, 24) };

        // First run: creates exactly 1 repost for itemCorrupt at badSle's date/time, skips itemPending
        await job.ExecuteAsync(args);
        insertedReposts.Count.ShouldBe(1);
        insertedReposts[0].ItemId.ShouldBe(itemCorrupt);
        insertedReposts[0].WarehouseId.ShouldBe(whId);
        insertedReposts[0].PostingDate.ShouldBe(new DateTime(2026, 5, 10));
        insertedReposts[0].PostingTime.ShouldBe(new TimeSpan(14, 30, 0));
        insertedReposts[0].VoucherType.ShouldBe(StockLedgerAuditJob.SchedulerVoucherType);

        // Second run: deduplicates against the existing scheduler repost (even after completion)
        insertedReposts[0].StartProcessing();
        insertedReposts[0].Complete(1);
        await job.ExecuteAsync(args);
        insertedReposts.Count.ShouldBe(1);
    }
}
