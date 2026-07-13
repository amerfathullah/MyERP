using System;
using MyERP.HumanResources.Entities;
using Shouldly;
using Xunit;

namespace MyERP.HR;

public class LeaveAllocationTests
{
    [Fact]
    public void LeaveAllocation_Create()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);
        alloc.TotalLeavesAllocated.ShouldBe(12);
        alloc.Balance.ShouldBe(12);
        alloc.LeavesUsed.ShouldBe(0);
    }

    [Fact]
    public void LeaveAllocation_DeductLeave()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);
        alloc.DeductLeave(3);
        alloc.LeavesUsed.ShouldBe(3);
        alloc.Balance.ShouldBe(9);
    }

    [Fact]
    public void LeaveAllocation_RestoreLeave()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);
        alloc.DeductLeave(5);
        alloc.RestoreLeave(2);
        alloc.LeavesUsed.ShouldBe(3);
        alloc.Balance.ShouldBe(9);
    }

    [Fact]
    public void LeaveAllocation_RestoreLeave_NeverNegative()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);
        alloc.DeductLeave(2);
        alloc.RestoreLeave(10); // More than used
        alloc.LeavesUsed.ShouldBe(0);
        alloc.Balance.ShouldBe(12);
    }

    [Fact]
    public void LeaveAllocation_CarryForward_IncreaseBalance()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);
        alloc.CarryForwardDays = 5;
        alloc.Balance.ShouldBe(17); // 12 + 5
    }

    [Fact]
    public void LeaveAllocation_FullyUsed_ZeroBalance()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);
        alloc.DeductLeave(12);
        alloc.Balance.ShouldBe(0);
    }

    [Fact]
    public void LeaveAllocation_NewLeavesAllocated_ExcludesCarryForward()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);
        alloc.CarryForwardDays = 5;
        alloc.DeductLeave(8);
        // NewLeavesAllocated = 12 - 8 = 4 (carry forward not counted)
        alloc.NewLeavesAllocated.ShouldBe(4);
    }
}
