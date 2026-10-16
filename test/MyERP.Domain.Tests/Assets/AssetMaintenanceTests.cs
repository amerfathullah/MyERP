using System;
using MyERP.Maintenance.Entities;
using MyERP.Maintenance;
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

    [Fact]
    public void AssetMaintenanceTeam_DuplicateMember_ThrowsValidationFailed()
    {
        var team = new AssetMaintenanceTeam(Guid.NewGuid(), Guid.NewGuid(), "Duplicate Member Team");
        var empId = Guid.NewGuid();

        var ex = Should.Throw<BusinessException>(() =>
            team.SetMembers(new (Guid EmployeeId, string? MaintenanceRole)[]
            {
                (empId, "Technician"),
                (empId, "Senior Technician"),
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void AssetMaintenanceTeam_ManagerNotMember_ThrowsMaintenanceManagerMustBeMember()
    {
        var team = new AssetMaintenanceTeam(Guid.NewGuid(), Guid.NewGuid(), "Manager Validation Team")
        {
            MaintenanceManagerId = Guid.NewGuid(),
        };
        team.SetMembers(new (Guid EmployeeId, string? MaintenanceRole)[]
        {
            (Guid.NewGuid(), "Technician"),
        });

        var ex = Should.Throw<BusinessException>(() => team.Validate());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.MaintenanceManagerMustBeMember);
    }

    [Fact]
    public void AssetMaintenanceTeam_ManagerIsMember_PassesValidation()
    {
        var managerId = Guid.NewGuid();
        var team = new AssetMaintenanceTeam(Guid.NewGuid(), Guid.NewGuid(), "Valid Team")
        {
            MaintenanceManagerId = managerId,
        };
        team.SetMembers(new (Guid EmployeeId, string? MaintenanceRole)[]
        {
            (managerId, "Manager"),
            (Guid.NewGuid(), "Technician"),
        });

        Should.NotThrow(() => team.Validate());
    }

    [Fact]
    public void CalculateNextDueDate_RespectsEndDate()
    {
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc);

        // Monthly from Jan 1 exceeds Jan 20 -> returns null
        AssetMaintenanceTask.CalculateNextDueDate(MaintenancePeriodicity.Monthly, startDate, endDate)
            .ShouldBeNull();

        // Weekly from Jan 1 is Jan 8 <= Jan 20 -> returns Jan 8
        AssetMaintenanceTask.CalculateNextDueDate(MaintenancePeriodicity.Weekly, startDate, endDate)
            .ShouldBe(new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void CalculateNextDueDate_RespectsLastCompletionDate()
    {
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastCompletionDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);

        // Next due date calculated from lastCompletionDate
        AssetMaintenanceTask.CalculateNextDueDate(
            MaintenancePeriodicity.Monthly, startDate, lastCompletionDate: lastCompletionDate)
            .ShouldBe(new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void AssetMaintenance_Validate_DraftAsset_ThrowsAssetMustBeSubmitted()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-01", "Draft Machine", DateTime.UtcNow, 1000m);
        var maintenance = new AssetMaintenance(Guid.NewGuid(), Guid.NewGuid(), asset.Id);

        var ex = Should.Throw<BusinessException>(() => maintenance.Validate(asset));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.AssetMustBeSubmitted);
    }

    [Fact]
    public void AssetMaintenance_Validate_StartDateGeEndDate_ThrowsStartDateMustBeLessThanEndDate()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-01", "Machine", DateTime.UtcNow, 1000m);
        asset.Submit();

        var maintenance = new AssetMaintenance(Guid.NewGuid(), Guid.NewGuid(), asset.Id);
        maintenance.AddTask(
            "Task 1",
            MaintenancePeriodicity.Monthly,
            startDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        var ex = Should.Throw<BusinessException>(() => maintenance.Validate(asset));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.StartDateMustBeLessThanEndDate);
    }

    [Fact]
    public void AssetMaintenance_Validate_NextDueDateLtStartDate_ThrowsNextDueDateCannotBeBeforeStartDate()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-01", "Machine", DateTime.UtcNow, 1000m);
        asset.Submit();

        var maintenance = new AssetMaintenance(Guid.NewGuid(), Guid.NewGuid(), asset.Id);
        maintenance.AddTask(
            "Task 1",
            MaintenancePeriodicity.Monthly,
            startDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            nextDueDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        var ex = Should.Throw<BusinessException>(() => maintenance.Validate(asset));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.NextDueDateCannotBeBeforeStartDate);
    }

    [Fact]
    public void AssetMaintenance_Validate_AssigneeNotInTeam_ThrowsAssigneeNotInMaintenanceTeam()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-01", "Machine", DateTime.UtcNow, 1000m);
        asset.Submit();

        var maintenance = new AssetMaintenance(Guid.NewGuid(), Guid.NewGuid(), asset.Id);
        var task = maintenance.AddTask(
            "Task 1",
            MaintenancePeriodicity.Monthly,
            startDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        task.AssignTo = "NonMemberUser";

        var teamNames = new[] { "MemberA", "MemberB" };
        var ex = Should.Throw<BusinessException>(() => maintenance.Validate(asset, teamMemberNames: teamNames));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.AssigneeNotInMaintenanceTeam);
    }

    [Fact]
    public void AssetMaintenance_Validate_CancelledTaskAssigneeNotInTeam_Passes()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-01", "Machine", DateTime.UtcNow, 1000m);
        asset.Submit();

        var maintenance = new AssetMaintenance(Guid.NewGuid(), Guid.NewGuid(), asset.Id);
        var task = maintenance.AddTask(
            "Task 1",
            MaintenancePeriodicity.Monthly,
            startDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        task.AssignTo = "FormerMemberUser";
        task.MaintenanceStatus = AssetMaintenanceStatus.Cancelled;

        var teamNames = new[] { "MemberA", "MemberB" };
        Should.NotThrow(() => maintenance.Validate(asset, teamMemberNames: teamNames));
    }

    [Fact]
    public void AssetMaintenanceLog_CheckOverdue_ResetsToPlannedWhenFuture()
    {
        var log = new AssetMaintenanceLog(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Inspection",
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        // As of Feb 5, it is overdue
        log.CheckOverdue(new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc));
        log.Status.ShouldBe(AssetMaintenanceStatus.Overdue);

        // Due date updated to Feb 10, checked as of Feb 5 -> resets back to Planned
        log.DueDate = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc);
        log.CheckOverdue(new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc));
        log.Status.ShouldBe(AssetMaintenanceStatus.Planned);
    }

    [Fact]
    public void Asset_SetInMaintenance_And_ResetMaintenanceStatus()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-01", "Machine", DateTime.UtcNow, 1000m);
        asset.Submit();
        asset.Status.ShouldBe(AssetStatus.Submitted);

        asset.SetInMaintenance();
        asset.Status.ShouldBe(AssetStatus.InMaintenance);

        asset.ResetMaintenanceStatus();
        asset.Status.ShouldBe(AssetStatus.Submitted);
    }
}
