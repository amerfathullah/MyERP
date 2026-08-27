using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Unit tests for Repost Item Valuation restart, cancellation, and summary metrics.
/// Verifies rules from erpnext/stock/doctype/repost_item_valuation (#6004).
/// </summary>
public class RepostItemValuationWorkflowTests
{
    private readonly IRepository<RepostItemValuation, Guid> _repo = Substitute.For<IRepository<RepostItemValuation, Guid>>();
    private readonly RepostItemValuationAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    public RepostItemValuationWorkflowTests()
    {
        _appService = new RepostItemValuationAppService(_repo);
    }

    [Fact]
    public async Task RestartAsync_FailedRepost_ResetsToQueuedAndClearsErrors()
    {
        var repostId = Guid.NewGuid();
        var repost = new RepostItemValuation(
            repostId, _companyId, RepostMethod.ItemAndWarehouse,
            new DateTime(2026, 6, 1), _itemId, _warehouseId);
        repost.StartProcessing();
        repost.Fail("Database timeout on SLE cursor");

        Assert.Equal(RepostStatus.Failed, repost.Status);
        Assert.NotNull(repost.ErrorLog);

        _repo.GetAsync(repostId).Returns(Task.FromResult(repost));

        var result = await _appService.RestartAsync(repostId);

        Assert.Equal(RepostStatus.Queued, repost.Status);
        Assert.Null(repost.ErrorLog);
        Assert.Equal(0, repost.CurrentIndex);
        Assert.False(repost.IsDeduplicated);
    }

    [Fact]
    public async Task RestartAsync_CompletedRepost_ThrowsValidationException()
    {
        var repostId = Guid.NewGuid();
        var repost = new RepostItemValuation(
            repostId, _companyId, RepostMethod.ItemAndWarehouse,
            new DateTime(2026, 6, 1), _itemId, _warehouseId);
        repost.StartProcessing();
        repost.Complete(45);

        _repo.GetAsync(repostId).Returns(Task.FromResult(repost));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.RestartAsync(repostId));
        Assert.Equal(MyERPDomainErrorCodes.InvalidStatusTransition, ex.Code);
    }

    [Fact]
    public async Task CancelAsync_QueuedRepost_TransitionsToCancelled()
    {
        var repostId = Guid.NewGuid();
        var repost = new RepostItemValuation(
            repostId, _companyId, RepostMethod.ItemAndWarehouse,
            new DateTime(2026, 6, 1), _itemId, _warehouseId);

        _repo.GetAsync(repostId).Returns(Task.FromResult(repost));

        var result = await _appService.CancelAsync(repostId);

        Assert.Equal(RepostStatus.Cancelled, repost.Status);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsAccurateMetrics()
    {
        var repost1 = new RepostItemValuation(
            Guid.NewGuid(), _companyId, RepostMethod.ItemAndWarehouse,
            new DateTime(2026, 6, 1), _itemId, _warehouseId); // Queued

        var repost2 = new RepostItemValuation(
            Guid.NewGuid(), _companyId, RepostMethod.ItemAndWarehouse,
            new DateTime(2026, 6, 1), _itemId, _warehouseId);
        repost2.StartProcessing();
        repost2.Complete(25); // Completed

        var repost3 = new RepostItemValuation(
            Guid.NewGuid(), _companyId, RepostMethod.ItemAndWarehouse,
            new DateTime(2026, 6, 1), _itemId, _warehouseId);
        repost3.StartProcessing();
        repost3.Fail("Error"); // Failed

        var list = new List<RepostItemValuation> { repost1, repost2, repost3 };
        _repo.GetQueryableAsync().Returns(Task.FromResult(list.AsQueryable()));

        var summary = await _appService.GetSummaryAsync(_companyId);

        Assert.NotNull(summary);
        Assert.Equal(_companyId, summary.CompanyId);
        Assert.Equal(1, summary.QueuedCount);
        Assert.Equal(0, summary.InProgressCount);
        Assert.Equal(1, summary.CompletedCount);
        Assert.Equal(1, summary.FailedCount);
        Assert.Equal(25, summary.TotalEntriesProcessed);
    }
}
