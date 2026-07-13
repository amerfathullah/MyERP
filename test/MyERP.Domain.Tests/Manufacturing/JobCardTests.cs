using System;
using MyERP.Manufacturing.Entities;
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
}
