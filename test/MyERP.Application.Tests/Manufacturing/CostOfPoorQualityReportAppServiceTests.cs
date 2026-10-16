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

public class CostOfPoorQualityReportAppServiceTests
{
    private readonly IRepository<JobCard, Guid> _jobCardRepo = Substitute.For<IRepository<JobCard, Guid>>();
    private readonly IRepository<Operation, Guid> _operationRepo = Substitute.For<IRepository<Operation, Guid>>();
    private readonly IRepository<Workstation, Guid> _workstationRepo = Substitute.For<IRepository<Workstation, Guid>>();
    private readonly IRepository<WorkOrder, Guid> _workOrderRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();

    private readonly CostOfPoorQualityReportAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public CostOfPoorQualityReportAppServiceTests()
    {
        _appService = new CostOfPoorQualityReportAppService(
            _jobCardRepo,
            _operationRepo,
            _workstationRepo,
            _workOrderRepo,
            _itemRepo);
    }

    [Fact]
    public async Task GetReportAsync_CalculatesCostFromHourRateAndTime()
    {
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-REWORK", "Defective Cylinder", ItemType.Goods);

        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, _companyId, "WO-REWORK-01", itemId, Guid.NewGuid(), 10m);

        var opId = Guid.NewGuid();
        var op = new Operation(opId, "Polishing Rework");

        var wsId = Guid.NewGuid();
        var ws = new Workstation(wsId, _companyId, "WS-POLISH");
        ws.AddCost("Operating Cost", 100m); // HourRate = 100

        var jc = new JobCard(Guid.NewGuid(), _companyId, woId, opId, 10m, 1)
        {
            WorkstationId = wsId,
            IsCorrective = true,
            TotalTimeInMins = 90m
        };

        _operationRepo.GetListAsync(Arg.Any<Expression<Func<Operation, bool>>>())
            .Returns(Task.FromResult(new List<Operation> { op }));

        _jobCardRepo.WithDetailsAsync(Arg.Any<Expression<Func<JobCard, object>>[]>())
            .Returns(Task.FromResult(new List<JobCard> { jc }.AsQueryable()));

        _workOrderRepo.GetListAsync(Arg.Any<Expression<Func<WorkOrder, bool>>>())
            .Returns(Task.FromResult(new List<WorkOrder> { wo }));

        _workstationRepo.GetListAsync(Arg.Any<Expression<Func<Workstation, bool>>>())
            .Returns(Task.FromResult(new List<Workstation> { ws }));

        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { item }));

        var result = await _appService.GetReportAsync(new CostOfPoorQualityReportRequestDto
        {
            CompanyId = _companyId
        });

        result.TotalJobCards.ShouldBe(1);
        result.TotalTimeInMins.ShouldBe(90m);
        result.TotalOperatingCost.ShouldBe(150m); // 100 * 90 / 60 = 150

        var row = result.Rows[0];
        row.WorkOrderNumber.ShouldBe("WO-REWORK-01");
        row.ProductionItemCode.ShouldBe("ITEM-REWORK");
        row.OperationName.ShouldBe("Polishing Rework");
        row.WorkstationName.ShouldBe("WS-POLISH");
        row.HourRate.ShouldBe(100m);
        row.TotalTimeInMins.ShouldBe(90m);
        row.OperatingCost.ShouldBe(150m);
    }

    [Fact]
    public async Task GetReportAsync_IncludesJobCardsUnderCorrectiveOperation()
    {
        var opId = Guid.NewGuid();
        var op = new Operation(opId, "Grinding Fix")
        {
            IsCorrectiveOperation = true
        };

        var jc = new JobCard(Guid.NewGuid(), _companyId, Guid.NewGuid(), opId, 5m, 1)
        {
            IsCorrective = false, // Not marked directly on JC, but operation is corrective
            TotalTimeInMins = 60m
        };

        _operationRepo.GetListAsync(Arg.Any<Expression<Func<Operation, bool>>>())
            .Returns(Task.FromResult(new List<Operation> { op }));

        _jobCardRepo.WithDetailsAsync(Arg.Any<Expression<Func<JobCard, object>>[]>())
            .Returns(Task.FromResult(new List<JobCard> { jc }.AsQueryable()));

        _workOrderRepo.GetListAsync(Arg.Any<Expression<Func<WorkOrder, bool>>>())
            .Returns(Task.FromResult(new List<WorkOrder>()));
        _workstationRepo.GetListAsync(Arg.Any<Expression<Func<Workstation, bool>>>())
            .Returns(Task.FromResult(new List<Workstation>()));
        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item>()));

        var result = await _appService.GetReportAsync(new CostOfPoorQualityReportRequestDto
        {
            CompanyId = _companyId
        });

        result.TotalJobCards.ShouldBe(1);
        result.Rows[0].OperationName.ShouldBe("Grinding Fix");
    }

    [Fact]
    public async Task GetReportAsync_ExcludesNonCorrectiveJobCards()
    {
        var opId = Guid.NewGuid();
        var op = new Operation(opId, "Standard Assembly")
        {
            IsCorrectiveOperation = false
        };

        var jc = new JobCard(Guid.NewGuid(), _companyId, Guid.NewGuid(), opId, 5m, 1)
        {
            IsCorrective = false,
            TotalTimeInMins = 60m
        };

        _operationRepo.GetListAsync(Arg.Any<Expression<Func<Operation, bool>>>())
            .Returns(Task.FromResult(new List<Operation>())); // No corrective operations

        _jobCardRepo.WithDetailsAsync(Arg.Any<Expression<Func<JobCard, object>>[]>())
            .Returns(Task.FromResult(new List<JobCard> { jc }.AsQueryable()));

        _workOrderRepo.GetListAsync(Arg.Any<Expression<Func<WorkOrder, bool>>>())
            .Returns(Task.FromResult(new List<WorkOrder>()));
        _workstationRepo.GetListAsync(Arg.Any<Expression<Func<Workstation, bool>>>())
            .Returns(Task.FromResult(new List<Workstation>()));
        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item>()));

        var result = await _appService.GetReportAsync(new CostOfPoorQualityReportRequestDto
        {
            CompanyId = _companyId
        });

        result.TotalJobCards.ShouldBe(0);
        result.Rows.Count.ShouldBe(0);
    }
}
