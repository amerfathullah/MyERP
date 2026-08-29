using System;
using MyERP.Assets.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Assets;

public class AssetRepairTests
{
    [Fact]
    public void AssetRepair_Complete_CalculatesDowntime()
    {
        var failure = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var completion = new DateTime(2026, 8, 1, 14, 30, 0, DateTimeKind.Utc);

        var repair = new AssetRepair(
            Guid.NewGuid(), "AS-REP-001", Guid.NewGuid(), Guid.NewGuid())
        {
            FailureDate = failure,
            CompletionDate = completion
        };

        repair.Complete();

        repair.Status.ShouldBe(AssetRepairStatus.Completed);
        repair.Downtime.ShouldBe("4.5 Hrs");
    }

    [Fact]
    public void AssetRepair_SetDowntime_ClearsWhenNotCompleted()
    {
        var repair = new AssetRepair(
            Guid.NewGuid(), "AS-REP-002", Guid.NewGuid(), Guid.NewGuid())
        {
            FailureDate = DateTime.UtcNow.AddHours(-2),
            CompletionDate = DateTime.UtcNow,
            Downtime = "2.00 Hrs"
        };

        repair.SetDowntime(); // Status is Pending
        repair.Downtime.ShouldBeNull();
    }
}
