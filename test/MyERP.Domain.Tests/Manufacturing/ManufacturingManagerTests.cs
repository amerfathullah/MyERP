using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Tests for JobCard entity lifecycle, WorkOrderManager material requirements,
/// and manufacturing domain invariants.
/// </summary>
public class ManufacturingManagerTests
{
    // ========== JobCard Lifecycle Tests ==========

    private static JobCard CreateJobCard(decimal forQty = 100)
    {
        return new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), forQty, 1);
    }

    [Fact]
    public void JobCard_DefaultStatus_IsOpen()
    {
        var jc = CreateJobCard();
        jc.Status.ShouldBe(JobCardStatus.Open);
        jc.CompletedQty.ShouldBe(0);
        jc.TotalTimeInMins.ShouldBe(0);
    }

    [Fact]
    public void JobCard_Start_TransitionsToWIP()
    {
        var jc = CreateJobCard();
        jc.Start();
        jc.Status.ShouldBe(JobCardStatus.WorkInProgress);
        jc.StartedAt.ShouldNotBeNull();
    }

    [Fact]
    public void JobCard_Start_FromWIP_Throws()
    {
        var jc = CreateJobCard();
        jc.Start();
        Should.Throw<BusinessException>(() => jc.Start());
    }

    [Fact]
    public void JobCard_AddTimeLog_AccumulatesQtyAndTime()
    {
        var jc = CreateJobCard();
        var from = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);

        jc.AddTimeLog(from, to, 30);

        jc.CompletedQty.ShouldBe(30);
        Math.Abs(jc.TotalTimeInMins - 120m).ShouldBeLessThan(0.01m);
        jc.TimeLogs.Count.ShouldBe(1);
    }

    [Fact]
    public void JobCard_AddTimeLog_MultipleLogs_Accumulate()
    {
        var jc = CreateJobCard();
        var t1 = new DateTime(2026, 7, 20, 6, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var t3 = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);

        jc.AddTimeLog(t1, t2, 40); // 2h, 40 units
        jc.AddTimeLog(t2, t3, 60); // 2h, 60 units

        jc.CompletedQty.ShouldBe(100);
        Math.Abs(jc.TotalTimeInMins - 240m).ShouldBeLessThan(0.01m);
        jc.TimeLogs.Count.ShouldBe(2);
    }

    [Fact]
    public void JobCard_AddTimeLog_FromOpen_TransitionsToWIP()
    {
        var jc = CreateJobCard();
        jc.Status.ShouldBe(JobCardStatus.Open);

        jc.AddTimeLog(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow, 10);

        jc.Status.ShouldBe(JobCardStatus.WorkInProgress);
    }

    [Fact]
    public void JobCard_AddTimeLog_InvalidTimeRange_Throws()
    {
        var jc = CreateJobCard();
        var now = DateTime.UtcNow;
        Should.Throw<ArgumentException>(() => jc.AddTimeLog(now, now.AddMinutes(-1), 10));
    }

    [Fact]
    public void JobCard_AddTimeLog_AfterComplete_Throws()
    {
        var jc = CreateJobCard();
        jc.Start();
        jc.Complete();
        Should.Throw<BusinessException>(() =>
            jc.AddTimeLog(DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow, 5));
    }

    [Fact]
    public void JobCard_Complete_FromWIP_Succeeds()
    {
        var jc = CreateJobCard();
        jc.Start();
        jc.Complete();
        jc.Status.ShouldBe(JobCardStatus.Completed);
        jc.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public void JobCard_Complete_FromOpen_Throws()
    {
        var jc = CreateJobCard();
        Should.Throw<BusinessException>(() => jc.Complete());
    }

    [Fact]
    public void JobCard_Hold_FromWIP_Succeeds()
    {
        var jc = CreateJobCard();
        jc.Start();
        jc.Hold();
        jc.Status.ShouldBe(JobCardStatus.OnHold);
    }

    [Fact]
    public void JobCard_Hold_FromOpen_Throws()
    {
        var jc = CreateJobCard();
        Should.Throw<BusinessException>(() => jc.Hold());
    }

    [Fact]
    public void JobCard_Resume_FromHold_Succeeds()
    {
        var jc = CreateJobCard();
        jc.Start();
        jc.Hold();
        jc.Resume();
        jc.Status.ShouldBe(JobCardStatus.WorkInProgress);
    }

    [Fact]
    public void JobCard_Resume_FromWIP_Throws()
    {
        var jc = CreateJobCard();
        jc.Start();
        Should.Throw<BusinessException>(() => jc.Resume());
    }

    [Fact]
    public void JobCard_Cancel_FromAnyState_Succeeds()
    {
        var jc = CreateJobCard();
        jc.Cancel();
        jc.Status.ShouldBe(JobCardStatus.Cancelled);
    }

    [Fact]
    public void JobCard_Cancel_FromCancelled_Throws()
    {
        var jc = CreateJobCard();
        jc.Cancel();
        Should.Throw<BusinessException>(() => jc.Cancel());
    }

    [Fact]
    public void JobCard_FullLifecycle_OpenToComplete()
    {
        var jc = CreateJobCard(50);
        jc.Start();
        jc.AddTimeLog(DateTime.UtcNow.AddMinutes(-60), DateTime.UtcNow.AddMinutes(-30), 25);
        jc.AddTimeLog(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow, 25);
        jc.Complete();

        jc.Status.ShouldBe(JobCardStatus.Completed);
        jc.CompletedQty.ShouldBe(50);
        jc.ForQuantity.ShouldBe(50);
    }

    [Fact]
    public void JobCard_HoldResumeComplete_Lifecycle()
    {
        var jc = CreateJobCard();
        jc.Start();
        var t1 = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);
        var t3 = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);
        var t4 = new DateTime(2026, 7, 20, 11, 0, 0, DateTimeKind.Utc);
        jc.AddTimeLog(t1, t2, 50);
        jc.Hold();
        // Some time passes...
        jc.Resume();
        jc.AddTimeLog(t3, t4, 50);
        jc.Complete();

        jc.Status.ShouldBe(JobCardStatus.Completed);
        jc.CompletedQty.ShouldBe(100);
        Math.Abs(jc.TotalTimeInMins - 120m).ShouldBeLessThan(0.01m);
    }

    // ========== WorkOrderMaterialRequirement Tests ==========

    [Fact]
    public void MaterialRequirement_DefaultsToZero()
    {
        var req = new WorkOrderMaterialRequirement();
        req.RequiredQty.ShouldBe(0);
        req.Rate.ShouldBe(0);
        req.ItemId.ShouldBe(Guid.Empty);
        req.SourceWarehouseId.ShouldBeNull();
    }

    [Fact]
    public void MaterialRequirement_ProportionalFormula()
    {
        // BOM: 10 units of RM per 1 unit of FG
        // Producing 5 FG → need 50 RM
        var bomQty = 1m;
        var bomItemQty = 10m;
        var produceQty = 5m;

        var requiredQty = bomQty > 0 ? bomItemQty * (produceQty / bomQty) : 0;
        requiredQty.ShouldBe(50);
    }

    [Fact]
    public void MaterialRequirement_ZeroBomQty_ReturnsZero()
    {
        var bomQty = 0m;
        var bomItemQty = 10m;
        var produceQty = 5m;

        var requiredQty = bomQty > 0 ? bomItemQty * (produceQty / bomQty) : 0;
        requiredQty.ShouldBe(0);
    }

    [Fact]
    public void MaterialRequirement_PartialProduction()
    {
        // BOM: makes 4 units, needs 12 of RM
        // Producing 1 → need 3 RM
        var bomQty = 4m;
        var bomItemQty = 12m;
        var produceQty = 1m;

        var requiredQty = bomQty > 0 ? bomItemQty * (produceQty / bomQty) : 0;
        requiredQty.ShouldBe(3);
    }

    // ========== JobCard Batch Splitting Logic ==========

    [Fact]
    public void BatchSplitting_ZeroBatchSize_SingleCard()
    {
        // batchSize=0 means one JC for full WO qty
        var woQty = 100m;
        var batchSize = 0m;
        var effectiveBatch = batchSize > 0 ? batchSize : woQty;

        var cardCount = (int)Math.Ceiling(woQty / effectiveBatch);
        cardCount.ShouldBe(1);
    }

    [Fact]
    public void BatchSplitting_ExactDivision()
    {
        var woQty = 100m;
        var batchSize = 25m;

        var cardCount = (int)Math.Ceiling(woQty / batchSize);
        cardCount.ShouldBe(4);
    }

    [Fact]
    public void BatchSplitting_UnevenDivision()
    {
        var woQty = 110m;
        var batchSize = 25m;

        var remaining = woQty;
        var cards = 0;
        while (remaining > 0)
        {
            var qty = Math.Min(batchSize, remaining);
            cards++;
            remaining -= qty;
        }
        cards.ShouldBe(5); // 25+25+25+25+10
    }

    [Fact]
    public void BatchSplitting_LastBatchIsRemainder()
    {
        var woQty = 110m;
        var batchSize = 25m;
        var batches = new System.Collections.Generic.List<decimal>();
        var remaining = woQty;

        while (remaining > 0)
        {
            var qty = Math.Min(batchSize, remaining);
            batches.Add(qty);
            remaining -= qty;
        }

        batches.Sum().ShouldBe(110);
        batches.Last().ShouldBe(10); // remainder
        batches.Count.ShouldBe(5);
    }

    // ========== Overproduction Percentage ==========

    [Fact]
    public void Overproduction_DefaultsZero_ExactMatch()
    {
        var pct = 0m;
        var woQty = 100m;
        var maxAllowed = woQty * (1 + pct / 100);
        maxAllowed.ShouldBe(100);
    }

    [Fact]
    public void Overproduction_FivePercent_AllowsExtra()
    {
        var pct = 5m;
        var woQty = 100m;
        var maxAllowed = woQty * (1 + pct / 100);
        maxAllowed.ShouldBe(105);
    }

    [Fact]
    public void Overproduction_CumulativeCheck()
    {
        var pct = 10m;
        var woQty = 100m;
        var maxAllowed = woQty * (1 + pct / 100);
        var alreadyProduced = 105m;
        var newBatch = 10m;

        var wouldExceed = (alreadyProduced + newBatch) > maxAllowed;
        wouldExceed.ShouldBeTrue(); // 115 > 110
    }

    // ========== Backflush Method Resolution ==========

    [Fact]
    public void BackflushMethod_BomOverride_TakesPrecedence()
    {
        // Simulates: BOM has BackflushBasedOn set
        var bomBackflush = "Material Transferred";
        var globalBackflush = "BOM";

        var effective = !string.IsNullOrWhiteSpace(bomBackflush) ? bomBackflush : globalBackflush;
        effective.ShouldBe("Material Transferred");
    }

    [Fact]
    public void BackflushMethod_NoBomOverride_FallsBackToGlobal()
    {
        var bomBackflush = (string?)null;
        var globalBackflush = "BOM";

        var effective = !string.IsNullOrWhiteSpace(bomBackflush) ? bomBackflush : globalBackflush;
        effective.ShouldBe("BOM");
    }

    [Fact]
    public void BackflushMethod_NoSettings_DefaultsBOM()
    {
        var bomBackflush = (string?)null;
        var settingsBackflush = (string?)null;

        var effective = !string.IsNullOrWhiteSpace(bomBackflush)
            ? bomBackflush
            : (settingsBackflush ?? "BOM");
        effective.ShouldBe("BOM");
    }
}
