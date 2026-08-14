using System;
using MyERP.Assets.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Assets;

public class AssetMaintenanceTests
{
    [Fact]
    public void CalculateNextDueDate_CalculatesCorrectly()
    {
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        AssetMaintenanceTask.CalculateNextDueDate(MaintenancePeriodicity.Daily, baseDate)
            .ShouldBe(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        AssetMaintenanceTask.CalculateNextDueDate(MaintenancePeriodicity.Weekly, baseDate)
            .ShouldBe(new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc));

        AssetMaintenanceTask.CalculateNextDueDate(MaintenancePeriodicity.Monthly, baseDate)
            .ShouldBe(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        AssetMaintenanceTask.CalculateNextDueDate(MaintenancePeriodicity.Quarterly, baseDate)
            .ShouldBe(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        AssetMaintenanceTask.CalculateNextDueDate(MaintenancePeriodicity.HalfYearly, baseDate)
            .ShouldBe(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        AssetMaintenanceTask.CalculateNextDueDate(MaintenancePeriodicity.Yearly, baseDate)
            .ShouldBe(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        AssetMaintenanceTask.CalculateNextDueDate(MaintenancePeriodicity.TwoYearly, baseDate)
            .ShouldBe(new DateTime(2028, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        AssetMaintenanceTask.CalculateNextDueDate(MaintenancePeriodicity.ThreeYearly, baseDate)
            .ShouldBe(new DateTime(2029, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void AssetMaintenance_AddTask_AddsToCollection()
    {
        var am = new AssetMaintenance(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var task = am.AddTask("Oil Change", MaintenancePeriodicity.Monthly, startDate);

        am.Tasks.Count.ShouldBe(1);
        task.MaintenanceTask.ShouldBe("Oil Change");
        task.NextDueDate.ShouldBe(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void AssetMaintenanceLog_Complete_SetsStatusAndCompletionDate()
    {
        var log = new AssetMaintenanceLog(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Filter Cleaning",
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        log.Status.ShouldBe(AssetMaintenanceStatus.Planned);

        var completionDate = new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc);
        log.Complete(completionDate, "Cleaned air filters and intake mesh");

        log.Status.ShouldBe(AssetMaintenanceStatus.Completed);
        log.CompletionDate.ShouldBe(completionDate);
        log.ActionsPerformed.ShouldBe("Cleaned air filters and intake mesh");
    }

    [Fact]
    public void AssetMaintenanceLog_CheckOverdue_SetsOverdueIfPast()
    {
        var log = new AssetMaintenanceLog(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Inspection",
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        log.CheckOverdue(new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc));
        log.Status.ShouldBe(AssetMaintenanceStatus.Overdue);
    }
}
