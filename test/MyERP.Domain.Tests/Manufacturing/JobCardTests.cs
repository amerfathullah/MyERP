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
        jc.AddTimeLog(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, 100m);
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
        jc.AddTimeLog(DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), 100m);
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

    [Fact]
    public void CorrectiveJobCard_IsCorrective_DefaultFalse_CanBeSet()
    {
        // Per ERPNext PR #58091 / #58080:
        // Corrective job card transfers are excluded from standard Work Order transferred qty
        var jc = CreateJobCard();
        jc.IsCorrective.ShouldBeFalse();

        jc.IsCorrective = true;
        jc.IsCorrective.ShouldBeTrue();
    }

    [Fact]
    public void JobCard_TransferredQty_DefaultsToZero_CanBeSet()
    {
        var jc = CreateJobCard();
        jc.TransferredQty.ShouldBe(0m);

        jc.TransferredQty = 15.5m;
        jc.TransferredQty.ShouldBe(15.5m);
    }

    [Fact]
    public void UpdateTransferStatus_TransitionsStatusCorrectly()
    {
        var jc = CreateJobCard();
        jc.Status.ShouldBe(JobCardStatus.Open);

        // Any transferred -> PartiallyTransferred
        jc.UpdateTransferStatus(allTransferred: false, anyTransferred: true);
        jc.Status.ShouldBe(JobCardStatus.PartiallyTransferred);

        // All transferred -> MaterialTransferred
        jc.UpdateTransferStatus(allTransferred: true, anyTransferred: true);
        jc.Status.ShouldBe(JobCardStatus.MaterialTransferred);

        // Neither -> Open (e.g. transfer cancelled)
        jc.UpdateTransferStatus(allTransferred: false, anyTransferred: false);
        jc.Status.ShouldBe(JobCardStatus.Open);
    }

    [Fact]
    public void UpdateTransferStatus_DoesNotOverrideWorkInProgressOrCompleted()
    {
        var jc = CreateJobCard();
        jc.Start();
        jc.Status.ShouldBe(JobCardStatus.WorkInProgress);

        jc.UpdateTransferStatus(allTransferred: true, anyTransferred: true);
        jc.Status.ShouldBe(JobCardStatus.WorkInProgress);
    }

    [Fact]
    public void GetCompletionDefaults_NoPreviousOperation_DefaultsToPendingQty()
    {
        var jcRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JobCard, Guid>>();
        var wsRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Workstation, Guid>>();
        var manager = new MyERP.Manufacturing.DomainServices.JobCardManager(jcRepo, wsRepo);

        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, 1);
        var (completedQty, pendingQty) = manager.GetCompletionDefaults(jc, maxCompletableQty: null);

        completedQty.ShouldBe(10m);
        pendingQty.ShouldBe(0m);
    }

    [Fact]
    public void GetCompletionDefaults_PreviousOperationCapped_DefaultsToMaxCompletable()
    {
        var jcRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JobCard, Guid>>();
        var wsRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Workstation, Guid>>();
        var manager = new MyERP.Manufacturing.DomainServices.JobCardManager(jcRepo, wsRepo);

        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, 2);
        // Previous op produced 6 units, so max completable is 6
        var (completedQty, pendingQty) = manager.GetCompletionDefaults(jc, maxCompletableQty: 6m);

        completedQty.ShouldBe(6m);
        pendingQty.ShouldBe(4m);
    }

    [Fact]
    public void GetCompletionDefaults_PreviousOperationZero_DefaultsToZero_DoesNotFallbackToPending()
    {
        // Per ERPNext PR #60275 / commit 0f50d7d18d & 39d99e8cf1:
        // max_completable_qty = 0 is a hard limit and must not fall back to pending qty
        var jcRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JobCard, Guid>>();
        var wsRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Workstation, Guid>>();
        var manager = new MyERP.Manufacturing.DomainServices.JobCardManager(jcRepo, wsRepo);

        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, 2);
        var (completedQty, pendingQty) = manager.GetCompletionDefaults(jc, maxCompletableQty: 0m);

        completedQty.ShouldBe(0m);
        pendingQty.ShouldBe(10m);
    }

    // ========== ERPNext PR #60650: Capacity overlap, plain time logs, employee overlap tests ==========

    [Fact]
    public void CapacityOverlapDetection_SequentialAndOverlappingSlots()
    {
        // Mirrors ERPNext test_capacity_overlap_detection in test_job_card.py
        var sequential = new List<DomainServices.JobCardTimeLogRange>
        {
            new(new DateTime(2026, 1, 1, 10, 0, 0), new DateTime(2026, 1, 1, 11, 0, 0)),
            new(new DateTime(2026, 1, 1, 11, 0, 0), new DateTime(2026, 1, 1, 12, 0, 0)),
        };

        var overlapping = new List<DomainServices.JobCardTimeLogRange>
        {
            new(new DateTime(2026, 1, 1, 10, 0, 0), new DateTime(2026, 1, 1, 11, 0, 0)),
            new(new DateTime(2026, 1, 1, 10, 30, 0), new DateTime(2026, 1, 1, 11, 30, 0)),
        };

        // Sequential logs share one capacity slot; overlapping logs need two
        DomainServices.JobCardManager.GetAllotedCapacity(sequential).Count.ShouldBe(1);
        DomainServices.JobCardManager.GetAllotedCapacity(overlapping).Count.ShouldBe(2);

        // Capacity 1 overlaps with any log; capacity 2 only when both slots are taken
        DomainServices.JobCardManager.HasCapacityOverlap(1, sequential).ShouldBeTrue();
        DomainServices.JobCardManager.HasCapacityOverlap(2, sequential).ShouldBeFalse();
        DomainServices.JobCardManager.HasCapacityOverlap(2, overlapping).ShouldBeTrue();
    }

    [Fact]
    public void MergeTimeLogsPerJobCard_AcceptsPlainTimeLogs()
    {
        // Per ERPNext PR #60650 (commit 934d62446e): accept plain time logs in capacity check
        var plainLog1 = new DomainServices.JobCardTimeLogRange(new DateTime(2026, 1, 1, 10, 0, 0), new DateTime(2026, 1, 1, 11, 0, 0));
        var plainLog2 = new DomainServices.JobCardTimeLogRange(new DateTime(2026, 1, 1, 11, 0, 0), new DateTime(2026, 1, 1, 12, 0, 0));

        var merged = DomainServices.JobCardManager.MergeTimeLogsPerJobCard(new[] { plainLog1, plainLog2 });
        merged.Count.ShouldBe(2);
        merged[0].FromTime.ShouldBe(plainLog1.FromTime);
        merged[1].FromTime.ShouldBe(plainLog2.FromTime);
    }

    [Fact]
    public void MergeTimeLogsPerJobCard_MergesOverlappingLogsForSameCard()
    {
        var cardId = Guid.NewGuid();
        var log1 = new DomainServices.JobCardTimeLogRange(new DateTime(2026, 1, 1, 10, 0, 0), new DateTime(2026, 1, 1, 11, 0, 0), cardId);
        var log2 = new DomainServices.JobCardTimeLogRange(new DateTime(2026, 1, 1, 10, 30, 0), new DateTime(2026, 1, 1, 12, 0, 0), cardId);

        var merged = DomainServices.JobCardManager.MergeTimeLogsPerJobCard(new[] { log1, log2 });
        merged.Count.ShouldBe(1);
        merged[0].FromTime.ShouldBe(new DateTime(2026, 1, 1, 10, 0, 0));
        merged[0].ToTime.ShouldBe(new DateTime(2026, 1, 1, 12, 0, 0));
    }

    [Fact]
    public void ValidateEmployeeTimeOverlap_NonOverlapping_DoesNotThrow()
    {
        // Per ERPNext PR #60650 (commit b9ae627673): check employee overlap by time only
        // Employee with an earlier log (08:00 - 09:00) can log at 10:30 without error
        var otherLogs = new List<DomainServices.JobCardTimeLogRange>
        {
            new(new DateTime(2026, 4, 1, 8, 0, 0), new DateTime(2026, 4, 1, 9, 0, 0), Guid.NewGuid())
        };

        Should.NotThrow(() =>
            DomainServices.JobCardManager.ValidateEmployeeTimeOverlap(
                new DateTime(2026, 4, 1, 10, 30, 0),
                new DateTime(2026, 4, 1, 11, 30, 0),
                otherLogs,
                "EMP-001"));
    }

    [Fact]
    public void ValidateEmployeeTimeOverlap_Overlapping_Throws()
    {
        var otherLogs = new List<DomainServices.JobCardTimeLogRange>
        {
            new(new DateTime(2026, 4, 1, 10, 0, 0), new DateTime(2026, 4, 1, 11, 0, 0), Guid.NewGuid())
        };

        var ex = Should.Throw<BusinessException>(() =>
            DomainServices.JobCardManager.ValidateEmployeeTimeOverlap(
                new DateTime(2026, 4, 1, 10, 30, 0),
                new DateTime(2026, 4, 1, 11, 30, 0),
                otherLogs,
                "EMP-001"));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void AddTimeLog_MultipleOperatorsSimultaneous_MergesWorkstationMinutesOnce()
    {
        // Per ERPNext PR #60650 (commit f5ae5b18fa / test_two_operators_on_one_job_charge_the_workstation_once)
        var jc = CreateJobCard();
        var op1From = new DateTime(2026, 5, 1, 10, 0, 0);
        var op1To = new DateTime(2026, 5, 1, 11, 0, 0); // 60 mins
        var op2From = new DateTime(2026, 5, 1, 10, 0, 0);
        var op2To = new DateTime(2026, 5, 1, 11, 0, 0); // 60 mins

        jc.AddTimeLog(op1From, op1To, 10m);
        jc.AddTimeLog(op2From, op2To, 10m);

        jc.TotalTimeInMins.ShouldBe(60m); // Merged into 60 mins workstation time, not 120
        jc.CompletedQty.ShouldBe(20m);
    }

    [Fact]
    public void SetPendingQty_ExceedingRemainingQty_Throws()
    {
        // Per ERPNext PR #60650 (commit f5fae15b45 / test_pending_qty_cannot_exceed_what_is_left)
        var jc = CreateJobCard(); // ForQuantity = 100m
        jc.Start();
        jc.AddTimeLog(new DateTime(2026, 5, 1, 8, 0, 0), new DateTime(2026, 5, 1, 9, 0, 0), 80m); // completed = 80m, remaining = 20m

        var ex = Should.Throw<BusinessException>(() => jc.SetPendingQty(25m));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

        Should.NotThrow(() => jc.SetPendingQty(20m));
        jc.PendingQty.ShouldBe(20m);
    }

    [Fact]
    public void SetPendingQty_OnCompletedCard_Throws()
    {
        // Per ERPNext PR #60650 (commit 3548e53611 / test_submitted_job_card_time_logs_and_pending_qty_are_locked)
        var jc = CreateJobCard();
        jc.Start();
        jc.AddTimeLog(new DateTime(2026, 5, 1, 8, 0, 0), new DateTime(2026, 5, 1, 9, 0, 0), 100m);
        jc.Complete();

        Should.Throw<BusinessException>(() => jc.SetPendingQty(10m))
            .Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
    }

    [Fact]
    public async Task ValidateLaterOperationsAsync_LaterOperationHasHigherCompleted_Throws()
    {
        // Per ERPNext PR #60650 (commit e98a4a6aec / test_earlier_operation_cannot_be_undone_after_the_next_one)
        var woId = Guid.NewGuid();
        var jc1 = new JobCard(Guid.NewGuid(), Guid.NewGuid(), woId, Guid.NewGuid(), 10m, sequenceId: 1);
        jc1.Start();
        jc1.AddTimeLog(new DateTime(2026, 5, 1, 8, 0, 0), new DateTime(2026, 5, 1, 9, 0, 0), 10m);

        var jc2 = new JobCard(Guid.NewGuid(), Guid.NewGuid(), woId, Guid.NewGuid(), 10m, sequenceId: 2);
        jc2.Start();
        jc2.AddTimeLog(new DateTime(2026, 5, 1, 9, 0, 0), new DateTime(2026, 5, 1, 10, 0, 0), 10m);

        var jcRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JobCard, Guid>>();
        var wsRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Workstation, Guid>>();
        jcRepo.GetQueryableAsync().Returns(Task.FromResult(new List<JobCard> { jc1, jc2 }.AsQueryable()));

        var manager = new DomainServices.JobCardManager(jcRepo, wsRepo);

        // Cancelling sequence 1 would make its completed qty 0, but sequence 2 has completed 10
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateLaterOperationsAsync(jc1, targetCompletedQty: 0m));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }
}

