using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing.Services;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Inventory;

public class ValidateOperationsCompletedTests
{
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<WorkOrder, Guid> _woRepository;
    private readonly IRepository<JobCard, Guid> _jobCardRepository;
    private readonly StockEntryManager _manager;

    public ValidateOperationsCompletedTests()
    {
        _warehouseRepository = Substitute.For<IRepository<Warehouse, Guid>>();
        _woRepository = Substitute.For<IRepository<WorkOrder, Guid>>();
        _jobCardRepository = Substitute.For<IRepository<JobCard, Guid>>();
        _manager = new StockEntryManager(_warehouseRepository, null!, null!);
    }

    [Fact]
    public async Task ValidateOperationsCompleted_WhenCompletedQtySufficient_Succeeds()
    {
        var woId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-001", itemId, Guid.NewGuid(), 10m);
        _woRepository.FindAsync(woId).Returns(Task.FromResult<WorkOrder?>(wo));

        var opId = Guid.NewGuid();
        var jc = new JobCard(Guid.NewGuid(), wo.CompanyId, woId, opId, 10m, 1)
        {
            CompletedQty = 10m
        };

        var queryable = new List<JobCard> { jc }.AsQueryable();
        _jobCardRepository.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var se = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            WorkOrderId = woId,
            FgCompletedQty = 10m
        };

        await Should.NotThrowAsync(() => _manager.ValidateOperationsCompletedAsync(se, _woRepository, _jobCardRepository, 0m));
    }

    [Fact]
    public async Task ValidateOperationsCompleted_WhenCompletedQtyInsufficient_ThrowsOperationsNotComplete()
    {
        var woId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-001", itemId, Guid.NewGuid(), 10m);
        _woRepository.FindAsync(woId).Returns(Task.FromResult<WorkOrder?>(wo));

        var opId = Guid.NewGuid();
        var jc = new JobCard(Guid.NewGuid(), wo.CompanyId, woId, opId, 10m, 1)
        {
            CompletedQty = 5m
        };

        var queryable = new List<JobCard> { jc }.AsQueryable();
        _jobCardRepository.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var se = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            WorkOrderId = woId,
            FgCompletedQty = 8m
        };

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _manager.ValidateOperationsCompletedAsync(se, _woRepository, _jobCardRepository, 0m));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.OperationsNotComplete);
    }

    [Fact]
    public async Task ValidateOperationsCompleted_WithOverproductionAllowance_Passes()
    {
        var woId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-001", itemId, Guid.NewGuid(), 10m);
        _woRepository.FindAsync(woId).Returns(Task.FromResult<WorkOrder?>(wo));

        var opId = Guid.NewGuid();
        var jc = new JobCard(Guid.NewGuid(), wo.CompanyId, woId, opId, 10m, 1)
        {
            CompletedQty = 10m
        };

        var queryable = new List<JobCard> { jc }.AsQueryable();
        _jobCardRepository.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var se = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            WorkOrderId = woId,
            FgCompletedQty = 11m
        };

        // 10% allowance on 10 = 11 allowed
        await Should.NotThrowAsync(() => _manager.ValidateOperationsCompletedAsync(se, _woRepository, _jobCardRepository, 10m));
    }

    [Fact]
    public async Task ValidateOperationsCompleted_ExcludesCancelledAndCorrectiveJobCards()
    {
        var woId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-001", itemId, Guid.NewGuid(), 10m);
        _woRepository.FindAsync(woId).Returns(Task.FromResult<WorkOrder?>(wo));

        var opId = Guid.NewGuid();
        var activeJc = new JobCard(Guid.NewGuid(), wo.CompanyId, woId, opId, 10m, 1)
        {
            CompletedQty = 4m
        };
        var cancelledJc = new JobCard(Guid.NewGuid(), wo.CompanyId, woId, opId, 10m, 2)
        {
            CompletedQty = 10m
        };
        cancelledJc.Cancel();

        var correctiveJc = new JobCard(Guid.NewGuid(), wo.CompanyId, woId, opId, 10m, 3)
        {
            CompletedQty = 10m,
            IsCorrective = true
        };

        var queryable = new List<JobCard> { activeJc, cancelledJc, correctiveJc }.AsQueryable();
        _jobCardRepository.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var se = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            WorkOrderId = woId,
            FgCompletedQty = 5m
        };

        // Only activeJc (4m) counts, so 5m attempted should throw
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _manager.ValidateOperationsCompletedAsync(se, _woRepository, _jobCardRepository, 0m));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.OperationsNotComplete);
    }

    [Fact]
    public async Task ValidateOperationsCompleted_WhenTrackSemiFinishedGoods_SkipsValidation()
    {
        var woId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-001", itemId, Guid.NewGuid(), 10m)
        {
            TrackSemiFinishedGoods = true
        };
        _woRepository.FindAsync(woId).Returns(Task.FromResult<WorkOrder?>(wo));

        var opId = Guid.NewGuid();
        var jc = new JobCard(Guid.NewGuid(), wo.CompanyId, woId, opId, 10m, 1)
        {
            CompletedQty = 0m
        };

        var queryable = new List<JobCard> { jc }.AsQueryable();
        _jobCardRepository.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var se = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            WorkOrderId = woId,
            FgCompletedQty = 10m
        };

        await Should.NotThrowAsync(() => _manager.ValidateOperationsCompletedAsync(se, _woRepository, _jobCardRepository, 0m));
    }

    [Fact]
    public async Task WorkOrderProductionService_GetWorkOrderCompletedQty_ComputesBottleneck()
    {
        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 100m);
        _woRepository.GetAsync(woId).Returns(Task.FromResult(wo));

        var op1 = Guid.NewGuid();
        var op2 = Guid.NewGuid();

        var jc1 = new JobCard(Guid.NewGuid(), wo.CompanyId, woId, op1, 100m, 1) { CompletedQty = 80m };
        var jc2 = new JobCard(Guid.NewGuid(), wo.CompanyId, woId, op2, 100m, 2) { CompletedQty = 45m };

        var queryable = new List<JobCard> { jc1, jc2 }.AsQueryable();
        _jobCardRepository.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var prodService = new WorkOrderProductionService(_woRepository, _jobCardRepository);
        var bottleneckQty = await prodService.GetWorkOrderCompletedQtyAsync(woId);

        bottleneckQty.ShouldBe(45m);
    }

    [Fact]
    public void WorkOrder_Submit_WhenSkipTransferTrue_RemainsSubmittedStatus()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-SKIP-01", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            SkipTransfer = true
        };

        wo.Status.ShouldBe(WorkOrderStatus.Draft);
        wo.Submit();
        wo.Status.ShouldBe(WorkOrderStatus.Submitted);
    }
}
