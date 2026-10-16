using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
/// Unit tests for JobCardSummaryAppService per ERPNext PR #60656 (commit 114526ddec):
/// 1. Row status preserves stored JobCardStatus (does NOT overwrite non-completed cards to "Open").
/// 2. Chart labels include period boundaries and empty months (initialized with 0).
/// 3. Date boundary filtering is inclusive: from_date <= posting_date <= to_date.
/// 4. Chart datasets group cards into "Open" (all non-completed) and "Completed".
/// </summary>
public class JobCardSummaryAppServiceTests
{
    private readonly IRepository<JobCard, Guid> _jobCardRepo = Substitute.For<IRepository<JobCard, Guid>>();
    private readonly IRepository<WorkOrder, Guid> _workOrderRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Workstation, Guid> _workstationRepo = Substitute.For<IRepository<Workstation, Guid>>();
    private readonly IRepository<Operation, Guid> _operationRepo = Substitute.For<IRepository<Operation, Guid>>();

    private readonly JobCardSummaryAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();

    public JobCardSummaryAppServiceTests()
    {
        _appService = new JobCardSummaryAppService(
            _jobCardRepo,
            _workOrderRepo,
            _itemRepo,
            _workstationRepo,
            _operationRepo);
    }

    [Fact]
    public async Task GetSummaryAsync_PreservesStoredStatus_DoesNotOverwriteToOpen()
    {
        // Job card with WorkInProgress status
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-01", "Widget", ItemType.Goods);

        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, _companyId, "WO-001", itemId, Guid.NewGuid(), 10m);

        var opId = Guid.NewGuid();
        var op = new Operation(opId, "Assembly");

        var jc1 = new JobCard(Guid.NewGuid(), _companyId, woId, opId, 10m, 1);
        jc1.Start(); // Moves to WorkInProgress
        jc1.CreationTime = new DateTime(2026, 1, 15);

        var jc2 = new JobCard(Guid.NewGuid(), _companyId, woId, opId, 10m, 2);
        jc2.Start();
        jc2.CompletedQty = 10m;
        jc2.Complete(); // Completed
        jc2.CreationTime = new DateTime(2026, 1, 20);

        var jobCards = new List<JobCard> { jc1, jc2 };
        _jobCardRepo.WithDetailsAsync(Arg.Any<Expression<Func<JobCard, object>>[]>())
            .Returns(Task.FromResult(jobCards.AsQueryable()));

        _workOrderRepo.GetListAsync(Arg.Any<Expression<Func<WorkOrder, bool>>>())
            .Returns(Task.FromResult(new List<WorkOrder> { wo }));
        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { item }));
        _operationRepo.GetListAsync(Arg.Any<Expression<Func<Operation, bool>>>())
            .Returns(Task.FromResult(new List<Operation> { op }));
        _workstationRepo.GetListAsync(Arg.Any<Expression<Func<Workstation, bool>>>())
            .Returns(Task.FromResult(new List<Workstation>()));

        var result = await _appService.GetSummaryAsync(new JobCardSummaryRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        result.Rows.Count.ShouldBe(2);

        // Per ERPNext PR #60656: row status must preserve actual stored status (WorkInProgress), not overridden to "Open"
        var wipRow = result.Rows.First(r => r.Id == jc1.Id);
        wipRow.Status.ShouldBe(nameof(JobCardStatus.WorkInProgress));
        wipRow.StatusCode.ShouldBe(JobCardStatus.WorkInProgress);

        var completedRow = result.Rows.First(r => r.Id == jc2.Id);
        completedRow.Status.ShouldBe(nameof(JobCardStatus.Completed));
        completedRow.StatusCode.ShouldBe(JobCardStatus.Completed);
    }

    [Fact]
    public async Task GetSummaryAsync_Chart_IncludesPeriodBoundariesAndEmptyMonths()
    {
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-01", "Widget", ItemType.Goods);
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", itemId, Guid.NewGuid(), 10m);
        var op = new Operation(Guid.NewGuid(), "Cutting");

        // Test boundary conditions matching PR #60656 test_chart_includes_period_boundaries_and_empty_months:
        // Filter: 2026-01-01 to 2026-02-28
        // Card 1 on 2026-01-01 (from_date boundary) with WorkInProgress
        // Card 2 on 2026-01-31 (month end boundary) with Completed
        var jc1 = new JobCard(Guid.NewGuid(), _companyId, wo.Id, op.Id, 5m, 1);
        jc1.Start();
        jc1.CreationTime = new DateTime(2026, 1, 1);

        var jc2 = new JobCard(Guid.NewGuid(), _companyId, wo.Id, op.Id, 5m, 2);
        jc2.Start();
        jc2.CompletedQty = 5m;
        jc2.Complete();
        jc2.CreationTime = new DateTime(2026, 1, 31);

        var jobCards = new List<JobCard> { jc1, jc2 };
        _jobCardRepo.WithDetailsAsync(Arg.Any<Expression<Func<JobCard, object>>[]>())
            .Returns(Task.FromResult(jobCards.AsQueryable()));

        _workOrderRepo.GetListAsync(Arg.Any<Expression<Func<WorkOrder, bool>>>())
            .Returns(Task.FromResult(new List<WorkOrder> { wo }));
        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { item }));
        _operationRepo.GetListAsync(Arg.Any<Expression<Func<Operation, bool>>>())
            .Returns(Task.FromResult(new List<Operation> { op }));
        _workstationRepo.GetListAsync(Arg.Any<Expression<Func<Workstation, bool>>>())
            .Returns(Task.FromResult(new List<Workstation>()));

        var result = await _appService.GetSummaryAsync(new JobCardSummaryRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 2, 28)
        });

        // 2 monthly periods: Jan 2026 and Feb 2026
        result.Chart.Labels.Count.ShouldBe(2);
        result.Chart.Labels[0].ShouldBe("Jan 2026");
        result.Chart.Labels[1].ShouldBe("Feb 2026");

        var openDataset = result.Chart.Datasets.First(d => d.Name == "Open");
        var completedDataset = result.Chart.Datasets.First(d => d.Name == "Completed");

        // Jan has 1 Open (WorkInProgress) and 1 Completed. Feb has 0 Open and 0 Completed.
        openDataset.Values.ShouldBe(new List<int> { 1, 0 });
        completedDataset.Values.ShouldBe(new List<int> { 1, 0 });
    }

    [Fact]
    public async Task GetSummaryAsync_DateFilter_ExcludesOutOfRange()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m);
        var op = new Operation(Guid.NewGuid(), "Drilling");

        // Past card before filter window
        var jcPast = new JobCard(Guid.NewGuid(), _companyId, wo.Id, op.Id, 5m, 1);
        jcPast.CreationTime = new DateTime(2025, 12, 31);

        // Current card in filter window
        var jcCurrent = new JobCard(Guid.NewGuid(), _companyId, wo.Id, op.Id, 5m, 2);
        jcCurrent.CreationTime = new DateTime(2026, 1, 15);

        var jobCards = new List<JobCard> { jcPast, jcCurrent };
        _jobCardRepo.WithDetailsAsync(Arg.Any<Expression<Func<JobCard, object>>[]>())
            .Returns(Task.FromResult(jobCards.AsQueryable()));

        _workOrderRepo.GetListAsync(Arg.Any<Expression<Func<WorkOrder, bool>>>())
            .Returns(Task.FromResult(new List<WorkOrder> { wo }));
        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item>()));
        _operationRepo.GetListAsync(Arg.Any<Expression<Func<Operation, bool>>>())
            .Returns(Task.FromResult(new List<Operation> { op }));
        _workstationRepo.GetListAsync(Arg.Any<Expression<Func<Workstation, bool>>>())
            .Returns(Task.FromResult(new List<Workstation>()));

        var result = await _appService.GetSummaryAsync(new JobCardSummaryRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        result.Rows.Count.ShouldBe(1);
        result.Rows[0].Id.ShouldBe(jcCurrent.Id);
    }

    [Fact]
    public async Task GetSummaryAsync_CalculatesTimeLogMinMax()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m);
        var op = new Operation(Guid.NewGuid(), "Milling");

        var jc = new JobCard(Guid.NewGuid(), _companyId, wo.Id, op.Id, 10m, 1);
        jc.CreationTime = new DateTime(2026, 3, 10);
        jc.Start();
        jc.AddTimeLog(new DateTime(2026, 3, 10, 8, 0, 0), new DateTime(2026, 3, 10, 10, 0, 0), 4m);
        jc.AddTimeLog(new DateTime(2026, 3, 10, 11, 0, 0), new DateTime(2026, 3, 10, 14, 0, 0), 6m);

        var jobCards = new List<JobCard> { jc };
        _jobCardRepo.WithDetailsAsync(Arg.Any<Expression<Func<JobCard, object>>[]>())
            .Returns(Task.FromResult(jobCards.AsQueryable()));

        _workOrderRepo.GetListAsync(Arg.Any<Expression<Func<WorkOrder, bool>>>())
            .Returns(Task.FromResult(new List<WorkOrder> { wo }));
        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item>()));
        _operationRepo.GetListAsync(Arg.Any<Expression<Func<Operation, bool>>>())
            .Returns(Task.FromResult(new List<Operation> { op }));
        _workstationRepo.GetListAsync(Arg.Any<Expression<Func<Workstation, bool>>>())
            .Returns(Task.FromResult(new List<Workstation>()));

        var result = await _appService.GetSummaryAsync(new JobCardSummaryRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 3, 1),
            ToDate = new DateTime(2026, 3, 31)
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.FromTime.ShouldBe(new DateTime(2026, 3, 10, 8, 0, 0));
        row.ToTime.ShouldBe(new DateTime(2026, 3, 10, 14, 0, 0));
        row.TotalTimeInMins.ShouldBe(300m); // 120 + 180 mins
    }
}
