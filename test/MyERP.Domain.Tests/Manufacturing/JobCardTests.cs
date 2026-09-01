using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Manufacturing;

public class JobCardTests
{
    private static JobCard CreateJobCard() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, 10);

    [Fact]
    public void Create_SetsDefaults()
    {
        var jc = CreateJobCard();
        jc.Status.ShouldBe(JobCardStatus.Open);
        jc.CompletedQty.ShouldBe(0);
        jc.TotalTimeInMins.ShouldBe(0);
        jc.ForQuantity.ShouldBe(100m);
    }

    [Fact]
    public void Start_FromOpen_Succeeds()
    {
        var jc = CreateJobCard();
        jc.Start();
        jc.Status.ShouldBe(JobCardStatus.WorkInProgress);
        jc.StartedAt.ShouldNotBeNull();
    }

    [Fact]
    public void AddTimeLog_UpdatesTotals()
    {
        var jc = CreateJobCard();
        var from = new DateTime(2026, 7, 12, 8, 0, 0);
        var to = new DateTime(2026, 7, 12, 10, 0, 0); // 2 hours = 120 mins
        jc.AddTimeLog(from, to, 25m);

        jc.TotalTimeInMins.ShouldBe(120m);
        jc.CompletedQty.ShouldBe(25m);
        jc.Status.ShouldBe(JobCardStatus.WorkInProgress);
    }

    [Fact]
    public void AddTimeLog_InvalidTimeRange_Throws()
    {
        var jc = CreateJobCard();
        var from = new DateTime(2026, 7, 12, 10, 0, 0);
        var to = new DateTime(2026, 7, 12, 8, 0, 0);
        Should.Throw<ArgumentException>(() => jc.AddTimeLog(from, to, 10m));
    }

    [Fact]
    public void AddTimeLog_MultipleEntries_Accumulates()
    {
        var jc = CreateJobCard();
        jc.AddTimeLog(new DateTime(2026, 7, 12, 8, 0, 0), new DateTime(2026, 7, 12, 9, 0, 0), 20m);
        jc.AddTimeLog(new DateTime(2026, 7, 12, 9, 30, 0), new DateTime(2026, 7, 12, 11, 0, 0), 30m);
        jc.CompletedQty.ShouldBe(50m);
        jc.TotalTimeInMins.ShouldBe(150m);
        jc.TimeLogs.Count.ShouldBe(2);
    }

    [Fact]
    public void Complete_FromWIP_Succeeds()
    {
        var jc = CreateJobCard();
        jc.Start();
        jc.Complete();
        jc.Status.ShouldBe(JobCardStatus.Completed);
        jc.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Complete_FromOpen_Throws()
    {
        var jc = CreateJobCard();
        Should.Throw<BusinessException>(() => jc.Complete());
    }

    [Fact]
    public void Hold_And_Resume()
    {
        var jc = CreateJobCard();
        jc.Start();
        jc.Hold();
        jc.Status.ShouldBe(JobCardStatus.OnHold);
        jc.Resume();
        jc.Status.ShouldBe(JobCardStatus.WorkInProgress);
    }

    [Fact]
    public void Cancel_AnyState()
    {
        var jc = CreateJobCard();
        jc.Start();
        jc.Cancel();
        jc.Status.ShouldBe(JobCardStatus.Cancelled);
    }

    [Fact]
    public void AddTimeLog_WhenCompleted_Throws()
    {
        var jc = CreateJobCard();
        jc.Start();
        jc.Complete();
        Should.Throw<BusinessException>(() =>
            jc.AddTimeLog(DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 10m));
    }

    [Fact]
    public void JobCard_PendingQty_DefaultZero_CanBeSet()
    {
        var jc = CreateJobCard();
        jc.PendingQty.ShouldBe(0m);

        jc.PendingQty = 30m;
        jc.PendingQty.ShouldBe(30m);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetTotalJobCardQtyAsync_SubtractsPendingQty()
    {
        var jcRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JobCard, Guid>>();
        var wsRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Workstation, Guid>>();
        var manager = new MyERP.Manufacturing.DomainServices.JobCardManager(jcRepo, wsRepo);

        var woId = Guid.NewGuid();
        var opId = Guid.NewGuid();

        var jc1 = new JobCard(Guid.NewGuid(), Guid.NewGuid(), woId, opId, 10m, 1)
        {
            PendingQty = 3m // Net effective qty = 10 - 3 = 7
        };
        var jc2 = new JobCard(Guid.NewGuid(), Guid.NewGuid(), woId, opId, 5m, 2)
        {
            PendingQty = 0m // Net effective qty = 5 - 0 = 5
        };

        jcRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<JobCard> { jc1, jc2 }.AsQueryable()));

        var totalQty = await manager.GetTotalJobCardQtyAsync(woId, opId);
        totalQty.ShouldBe(12m); // (10 - 3) + (5 - 0) = 12
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateMaterialTransferAsync_ThrowsWhenNoMaterialsTransferred()
    {
        var jcRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JobCard, Guid>>();
        var wsRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Workstation, Guid>>();
        var woRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<WorkOrder, Guid>>();
        var manager = new MyERP.Manufacturing.DomainServices.JobCardManager(jcRepo, wsRepo);

        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m);
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), woId, Guid.NewGuid(), "Raw Mat 1", 10m)
        {
            TransferredQuantity = 0m,
        });

        woRepo.FindAsync(woId).Returns(System.Threading.Tasks.Task.FromResult<WorkOrder?>(wo));

        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), woId, Guid.NewGuid(), 10m, 1);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateMaterialTransferAsync(jc, woRepo));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateMaterialTransferAsync_SucceedsWhenMaterialsTransferred()
    {
        var jcRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JobCard, Guid>>();
        var wsRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Workstation, Guid>>();
        var woRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<WorkOrder, Guid>>();
        var manager = new MyERP.Manufacturing.DomainServices.JobCardManager(jcRepo, wsRepo);

        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m);
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), woId, Guid.NewGuid(), "Raw Mat 1", 10m)
        {
            TransferredQuantity = 10m,
        });

        woRepo.FindAsync(woId).Returns(System.Threading.Tasks.Task.FromResult<WorkOrder?>(wo));

        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), woId, Guid.NewGuid(), 10m, 1);

        await manager.ValidateMaterialTransferAsync(jc, woRepo);
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateMaterialTransferAsync_ThrowsWhenPartialMaterialsTransferred()
    {
        var jcRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JobCard, Guid>>();
        var wsRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Workstation, Guid>>();
        var woRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<WorkOrder, Guid>>();
        var manager = new MyERP.Manufacturing.DomainServices.JobCardManager(jcRepo, wsRepo);

        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m);
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), woId, Guid.NewGuid(), "Raw Mat 1", 10m)
        {
            TransferredQuantity = 5m, // Only partially transferred
        });

        woRepo.FindAsync(woId).Returns(System.Threading.Tasks.Task.FromResult<WorkOrder?>(wo));

        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), woId, Guid.NewGuid(), 10m, 1);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateMaterialTransferAsync(jc, woRepo));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateMaterialTransferAsync_WhenSkipTransfer_PassesWithoutTransfer()
    {
        var jcRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JobCard, Guid>>();
        var wsRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Workstation, Guid>>();
        var woRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<WorkOrder, Guid>>();
        var manager = new MyERP.Manufacturing.DomainServices.JobCardManager(jcRepo, wsRepo);

        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-002", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            SkipTransfer = true
        };
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), woId, Guid.NewGuid(), "Raw Mat 1", 10m)
        {
            TransferredQuantity = 0m,
        });

        woRepo.FindAsync(woId).Returns(System.Threading.Tasks.Task.FromResult<WorkOrder?>(wo));

        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), woId, Guid.NewGuid(), 10m, 1);

        await Should.NotThrowAsync(() => manager.ValidateMaterialTransferAsync(jc, woRepo));
    }

    [Fact]
    public async System.Threading.Tasks.Task GetMaxCompletableQtyAsync_CappedByPreviousOperation()
    {
        var jcRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JobCard, Guid>>();
        var wsRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Workstation, Guid>>();
        var manager = new MyERP.Manufacturing.DomainServices.JobCardManager(jcRepo, wsRepo);

        var woId = Guid.NewGuid();
        var jc1 = new JobCard(Guid.NewGuid(), Guid.NewGuid(), woId, Guid.NewGuid(), 10m, 1);
        jc1.Start();
        jc1.AddTimeLog(DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), 8m); // completed 8 of 10

        var jc2 = new JobCard(Guid.NewGuid(), Guid.NewGuid(), woId, Guid.NewGuid(), 10m, 2);

        var list = new List<JobCard> { jc1, jc2 }.AsQueryable();
        jcRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(list));

        // For sequence 1: returns null
        var max1 = await manager.GetMaxCompletableQtyAsync(jc1);
        max1.ShouldBeNull();

        // For sequence 2: capped at 8 (min previous completed = 8 - current 0 = 8)
        var max2 = await manager.GetMaxCompletableQtyAsync(jc2);
        max2.ShouldBe(8m);
    }
}
