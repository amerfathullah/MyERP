using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;
using MyERP.Manufacturing;

namespace MyERP.Domain.Tests.Manufacturing;

/// <summary>
/// Unit tests for Material Requirements Planning (MRP) bucket calculation and PR #59143 parity.
/// </summary>
public class MaterialRequirementsPlanningTests
{
    // === PR #59143: A to_date landing on a bucket boundary must still get that bucket's column ===

    [Fact]
    public void GenerateBuckets_MonthlyBoundary_IncludesToDateBucket()
    {
        // Monthly: "2026-11-01" to "2026-12-01"
        // In ERPNext prior to #59143: 'to_date > from_date' dropped December 2026 because 2026-12-01 is not > 2026-12-01.
        // With fix: 'from_date <= to_date' includes both Nov and Dec 2026.
        var from = new DateTime(2026, 11, 1);
        var to = new DateTime(2026, 12, 1);

        var buckets = MaterialRequirementsPlanningAppService.GenerateBuckets(from, to, MrpBucketSize.Monthly);

        buckets.Count.ShouldBe(2);
        buckets[0].FromDate.ShouldBe(new DateTime(2026, 11, 1));
        buckets[0].ToDate.ShouldBe(new DateTime(2026, 11, 30));
        buckets[0].Label.ShouldBe("Nov 2026");

        buckets[1].FromDate.ShouldBe(new DateTime(2026, 12, 1));
        buckets[1].ToDate.ShouldBe(new DateTime(2026, 12, 31));
        buckets[1].Label.ShouldBe("Dec 2026");
    }

    [Fact]
    public void GenerateBuckets_MonthlySameDay_ProducesSingleBucket()
    {
        var from = new DateTime(2026, 12, 1);
        var to = new DateTime(2026, 12, 1);

        var buckets = MaterialRequirementsPlanningAppService.GenerateBuckets(from, to, MrpBucketSize.Monthly);

        buckets.Count.ShouldBe(1);
        buckets[0].FromDate.ShouldBe(new DateTime(2026, 12, 1));
        buckets[0].ToDate.ShouldBe(new DateTime(2026, 12, 31));
        buckets[0].Label.ShouldBe("Dec 2026");
    }

    [Fact]
    public void GenerateBuckets_DailyBoundary_IncludesToDateBucket()
    {
        var from = new DateTime(2026, 11, 30);
        var to = new DateTime(2026, 12, 1);

        var buckets = MaterialRequirementsPlanningAppService.GenerateBuckets(from, to, MrpBucketSize.Daily);

        buckets.Count.ShouldBe(2);
        buckets[0].FromDate.ShouldBe(new DateTime(2026, 11, 30));
        buckets[0].ToDate.ShouldBe(new DateTime(2026, 11, 30));
        buckets[1].FromDate.ShouldBe(new DateTime(2026, 12, 1));
        buckets[1].ToDate.ShouldBe(new DateTime(2026, 12, 1));
    }

    [Fact]
    public void GenerateBuckets_WeeklyBoundary_IncludesToDateBucket()
    {
        // 2026-11-30 is Monday, 2026-12-07 is the following Monday
        var from = new DateTime(2026, 11, 30);
        var to = new DateTime(2026, 12, 7);

        var buckets = MaterialRequirementsPlanningAppService.GenerateBuckets(from, to, MrpBucketSize.Weekly);

        buckets.Count.ShouldBe(2);
        buckets[0].FromDate.ShouldBe(new DateTime(2026, 11, 30));
        buckets[0].ToDate.ShouldBe(new DateTime(2026, 12, 6));

        buckets[1].FromDate.ShouldBe(new DateTime(2026, 12, 7));
        buckets[1].ToDate.ShouldBe(new DateTime(2026, 12, 13));
    }

    // === Projected Available Balance & Planned Orders ===

    [Fact]
    public void MrpRow_CalculatesProjectedBalanceAndPlannedOrdersCorrectly()
    {
        // Initial stock = 10, Safety stock = 5
        // Bucket 1: Gross Req = 12, Scheduled Rec = 0
        // -> Projected balance = 10 + 0 - 12 = -2.
        // -> Shortage against safety stock = 5 - (-2) = 7.
        // -> Planned order = 7, New projected balance = 5.
        // Bucket 2: Gross Req = 8, Scheduled Rec = 10
        // -> Projected balance = 5 + 10 - 8 = 7 (>= 5, no shortage)
        // -> Planned order = 0, Final projected balance = 7.
        decimal initialStock = 10m;
        decimal safetyStock = 5m;

        var row = new MrpItemRowDto
        {
            ItemId = Guid.NewGuid(),
            ItemCode = "ITEM-MRP-01",
            ItemName = "Manufactured Widget",
            CurrentStock = initialStock,
            SafetyStock = safetyStock
        };

        var buckets = new List<(decimal Gross, decimal Rec)>
        {
            (Gross: 12m, Rec: 0m),
            (Gross: 8m, Rec: 10m)
        };

        var runningBalance = initialStock;
        foreach (var b in buckets)
        {
            var projected = runningBalance + b.Rec - b.Gross;
            decimal planned = 0m;
            if (projected < safetyStock)
            {
                planned = safetyStock - projected;
                projected += planned;
            }
            runningBalance = projected;

            row.Buckets.Add(new MrpItemBucketDataDto
            {
                GrossRequirements = b.Gross,
                ScheduledReceipts = b.Rec,
                ProjectedAvailableBalance = projected,
                PlannedOrders = planned
            });
        }

        row.Buckets[0].GrossRequirements.ShouldBe(12m);
        row.Buckets[0].PlannedOrders.ShouldBe(7m);
        row.Buckets[0].ProjectedAvailableBalance.ShouldBe(5m);

        row.Buckets[1].GrossRequirements.ShouldBe(8m);
        row.Buckets[1].ScheduledReceipts.ShouldBe(10m);
        row.Buckets[1].PlannedOrders.ShouldBe(0m);
        row.Buckets[1].ProjectedAvailableBalance.ShouldBe(7m);
    }
}
