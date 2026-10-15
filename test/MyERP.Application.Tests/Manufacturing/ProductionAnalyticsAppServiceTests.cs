using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Unit tests for ProductionAnalyticsAppService per ERPNext PR #60577 & PR #60581:
/// - Closed and Stopped work orders are excluded from overdue count.
/// - Closed work orders are tracked in status breakdown.
/// </summary>
public class ProductionAnalyticsAppServiceTests
{
    private readonly IRepository<WorkOrder, Guid> _workOrderRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly ProductionAnalyticsAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly DateTime _fromDate = new DateTime(2026, 1, 1);
    private readonly DateTime _toDate = new DateTime(2026, 12, 31);

    public ProductionAnalyticsAppServiceTests()
    {
        _appService = new ProductionAnalyticsAppService(_workOrderRepo, _itemRepo);
    }

    [Fact]
    public async Task GetAnalyticsAsync_ClosedAndStoppedWorkOrders_ExcludedFromOverdueCount()
    {
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-1", "Test Item", ItemType.Goods);

        var testDate = new DateTime(2026, 6, 1);

        // 1. Overdue in-process work order
        var overdueInProcess = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", itemId, Guid.NewGuid(), 10m)
        {
            PlannedEndDate = DateTime.UtcNow.AddDays(-5),
            CreationTime = testDate,
        };
        overdueInProcess.Submit();
        overdueInProcess.Start(); // InProcess

        // 2. Closed work order with past planned end date (should NOT be overdue)
        var closedWo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-002", itemId, Guid.NewGuid(), 10m)
        {
            PlannedEndDate = DateTime.UtcNow.AddDays(-10),
            CreationTime = testDate,
        };
        closedWo.Submit();
        closedWo.Close(); // Closed

        // 3. Stopped work order with past planned end date (should NOT be overdue)
        var stoppedWo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-003", itemId, Guid.NewGuid(), 10m)
        {
            PlannedEndDate = DateTime.UtcNow.AddDays(-12),
            CreationTime = testDate,
        };
        stoppedWo.Submit();
        stoppedWo.Start();
        stoppedWo.Stop(); // Stopped

        // 4. Completed work order with past planned end date (should NOT be overdue)
        var completedWo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-004", itemId, Guid.NewGuid(), 10m)
        {
            PlannedEndDate = DateTime.UtcNow.AddDays(-15),
            CreationTime = testDate,
        };
        completedWo.Submit();
        completedWo.Start();
        completedWo.RecordProduction(10m); // Completed

        var workOrders = new List<WorkOrder> { overdueInProcess, closedWo, stoppedWo, completedWo };

        _workOrderRepo.GetQueryableAsync().Returns(Task.FromResult(workOrders.AsQueryable()));
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var result = await _appService.GetAnalyticsAsync(_companyId, _fromDate, _toDate);

        // Only the in-process work order should be counted as overdue
        result.OverdueCount.ShouldBe(1);

        // Status breakdown should include Closed and Stopped
        var closedStatus = result.StatusBreakdown.FirstOrDefault(s => s.Status == "Closed");
        closedStatus.ShouldNotBeNull();
        closedStatus.Count.ShouldBe(1);

        var stoppedStatus = result.StatusBreakdown.FirstOrDefault(s => s.Status == "Stopped");
        stoppedStatus.ShouldNotBeNull();
        stoppedStatus.Count.ShouldBe(1);
    }
}
