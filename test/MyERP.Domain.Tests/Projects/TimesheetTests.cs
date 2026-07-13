using System;
using MyERP.Projects.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Projects;

public class TimesheetTests
{
    [Fact]
    public void Create_SetsDefaultStatus()
    {
        var ts = CreateTimesheet();
        ts.Status.ShouldBe(TimesheetStatus.Draft);
    }

    [Fact]
    public void AddDetail_RecalculatesTotals()
    {
        var ts = CreateTimesheet();
        ts.AddDetail(new TimesheetDetail(Guid.NewGuid(), ts.Id, "Development",
            new DateTime(2026, 7, 1, 9, 0, 0), new DateTime(2026, 7, 1, 17, 0, 0), 8)
        { IsBillable = true, BillingRate = 150, CostingRate = 80 });

        ts.TotalHours.ShouldBe(8m);
        ts.TotalBillableHours.ShouldBe(8m);
        ts.TotalBillingAmount.ShouldBe(1200m);
        ts.TotalCostingAmount.ShouldBe(640m);
    }

    [Fact]
    public void AddDetail_NonBillable_ExcludedFromBilling()
    {
        var ts = CreateTimesheet();
        ts.AddDetail(new TimesheetDetail(Guid.NewGuid(), ts.Id, "Admin",
            new DateTime(2026, 7, 1, 9, 0, 0), new DateTime(2026, 7, 1, 11, 0, 0), 2)
        { IsBillable = false, CostingRate = 60 });

        ts.TotalHours.ShouldBe(2m);
        ts.TotalBillableHours.ShouldBe(0m);
        ts.TotalBillingAmount.ShouldBe(0m);
        ts.TotalCostingAmount.ShouldBe(120m);
    }

    [Fact]
    public void Submit_WithDetails_Succeeds()
    {
        var ts = CreateTimesheet();
        ts.AddDetail(new TimesheetDetail(Guid.NewGuid(), ts.Id, "Dev",
            new DateTime(2026, 7, 1, 9, 0, 0), new DateTime(2026, 7, 1, 13, 0, 0), 4));

        ts.Submit();
        ts.Status.ShouldBe(TimesheetStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutDetails_Throws()
    {
        var ts = CreateTimesheet();
        Should.Throw<BusinessException>(() => ts.Submit());
    }

    [Fact]
    public void AddDetail_AfterSubmit_Throws()
    {
        var ts = CreateTimesheet();
        ts.AddDetail(new TimesheetDetail(Guid.NewGuid(), ts.Id, "Work",
            new DateTime(2026, 7, 1, 9, 0, 0), new DateTime(2026, 7, 1, 17, 0, 0), 8));
        ts.Submit();

        Should.Throw<BusinessException>(() =>
            ts.AddDetail(new TimesheetDetail(Guid.NewGuid(), ts.Id, "More",
                new DateTime(2026, 7, 2, 9, 0, 0), new DateTime(2026, 7, 2, 17, 0, 0), 8)));
    }

    [Fact]
    public void Cancel_Succeeds()
    {
        var ts = CreateTimesheet();
        ts.Cancel();
        ts.Status.ShouldBe(TimesheetStatus.Cancelled);
    }

    private static Timesheet CreateTimesheet() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 5));
}
