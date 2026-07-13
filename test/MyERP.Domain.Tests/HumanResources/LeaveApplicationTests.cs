using System;
using MyERP.HumanResources.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.HumanResources;

public class LeaveApplicationTests
{
    [Fact]
    public void Create_SetsOpenStatus()
    {
        var leave = CreateLeave(5);
        leave.Status.ShouldBe(LeaveApplicationStatus.Open);
        leave.TotalLeaveDays.ShouldBe(5m);
    }

    [Fact]
    public void Create_EndBeforeStart_Throws()
    {
        Should.Throw<BusinessException>(() =>
            new LeaveApplication(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                new DateTime(2026, 7, 10), new DateTime(2026, 7, 5), 5));
    }

    [Fact]
    public void Approve_FromOpen_Succeeds()
    {
        var leave = CreateLeave(3);
        leave.Approve();
        leave.Status.ShouldBe(LeaveApplicationStatus.Approved);
    }

    [Fact]
    public void Reject_FromOpen_Succeeds()
    {
        var leave = CreateLeave(1);
        leave.Reject();
        leave.Status.ShouldBe(LeaveApplicationStatus.Rejected);
    }

    [Fact]
    public void Approve_FromApproved_Throws()
    {
        var leave = CreateLeave(2);
        leave.Approve();
        Should.Throw<BusinessException>(() => leave.Approve());
    }

    [Fact]
    public void Cancel_Succeeds()
    {
        var leave = CreateLeave(1);
        leave.Approve();
        leave.Cancel();
        leave.Status.ShouldBe(LeaveApplicationStatus.Cancelled);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_Throws()
    {
        var leave = CreateLeave(1);
        leave.Cancel();
        Should.Throw<BusinessException>(() => leave.Cancel());
    }

    private static LeaveApplication CreateLeave(decimal days) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 8, 1), new DateTime(2026, 8, 1).AddDays((double)days - 1), days);
}
