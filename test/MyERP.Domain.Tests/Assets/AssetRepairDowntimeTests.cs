using System;
using MyERP.Assets.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Assets;

public class AssetRepairDowntimeTests
{
    [Fact]
    public void SetDowntime_WhenCompletedWithDates_CalculatesDowntimeHours()
    {
        var repair = new AssetRepair(Guid.NewGuid(), "AS-REP-001", Guid.NewGuid(), Guid.NewGuid())
        {
            FailureDate = new DateTime(2026, 7, 31, 9, 0, 0, DateTimeKind.Utc),
            CompletionDate = new DateTime(2026, 7, 31, 11, 0, 0, DateTimeKind.Utc)
        };

        repair.Complete();
        repair.Downtime.ShouldBe("2.0 Hrs");

        // Editing completion date refreshes downtime
        repair.CompletionDate = new DateTime(2026, 7, 31, 14, 30, 0, DateTimeKind.Utc);
        repair.SetDowntime();
        repair.Downtime.ShouldBe("5.5 Hrs");
    }

    [Fact]
    public void SetDowntime_WhenNotCompleted_ClearsDowntime()
    {
        var repair = new AssetRepair(Guid.NewGuid(), "AS-REP-002", Guid.NewGuid(), Guid.NewGuid())
        {
            FailureDate = new DateTime(2026, 7, 31, 9, 0, 0, DateTimeKind.Utc),
            CompletionDate = new DateTime(2026, 7, 31, 11, 0, 0, DateTimeKind.Utc),
            Downtime = "2.0 Hrs"
        };

        repair.SetDowntime();
        repair.Downtime.ShouldBeNull();
    }
}
