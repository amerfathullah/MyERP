using System;
using MyERP.HumanResources.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.HumanResources;

public class SalarySlipTests
{
    private static SalarySlip CreateSlip() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), DateTime.UtcNow);

    [Fact]
    public void Create_SetsDefaults()
    {
        var slip = CreateSlip();
        slip.Status.ShouldBe(Core.DocumentStatus.Draft);
        slip.GrossAmount.ShouldBe(0);
        slip.NetAmount.ShouldBe(0);
    }

    [Fact]
    public void AddEarning_CalculatesGross()
    {
        var slip = CreateSlip();
        slip.AddEarning(Guid.NewGuid(), "Basic", 5000m);
        slip.AddEarning(Guid.NewGuid(), "HRA", 1000m);
        slip.GrossAmount.ShouldBe(6000m);
        slip.NetAmount.ShouldBe(6000m);
    }

    [Fact]
    public void AddDeduction_CalculatesNet()
    {
        var slip = CreateSlip();
        slip.AddEarning(Guid.NewGuid(), "Basic", 5000m);
        slip.AddDeduction(Guid.NewGuid(), "EPF", 550m, isStatutory: true);
        slip.AddDeduction(Guid.NewGuid(), "SOCSO", 25m, isStatutory: true);
        slip.GrossAmount.ShouldBe(5000m);
        slip.TotalDeductions.ShouldBe(575m);
        slip.NetAmount.ShouldBe(4425m);
    }

    [Fact]
    public void Submit_Succeeds()
    {
        var slip = CreateSlip();
        slip.AddEarning(Guid.NewGuid(), "Basic", 5000m);
        slip.Submit();
        slip.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void AddEarning_AfterSubmit_Throws()
    {
        var slip = CreateSlip();
        slip.AddEarning(Guid.NewGuid(), "Basic", 5000m);
        slip.Submit();
        Should.Throw<BusinessException>(() => slip.AddEarning(Guid.NewGuid(), "Bonus", 1000m));
    }

    [Fact]
    public void Cancel_Submitted_Succeeds()
    {
        var slip = CreateSlip();
        slip.AddEarning(Guid.NewGuid(), "Basic", 5000m);
        slip.Submit();
        slip.Cancel();
        slip.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }
}

public class ExpenseClaimTests
{
    private static ExpenseClaim CreateClaim() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

    [Fact]
    public void Create_SetsDefaults()
    {
        var ec = CreateClaim();
        ec.Status.ShouldBe(Core.DocumentStatus.Draft);
        ec.TotalClaimedAmount.ShouldBe(0);
    }

    [Fact]
    public void AddExpense_SumsTotal()
    {
        var ec = CreateClaim();
        ec.AddExpense(DateTime.UtcNow, "Flight", 500m);
        ec.AddExpense(DateTime.UtcNow, "Hotel", 300m);
        ec.TotalClaimedAmount.ShouldBe(800m);
    }

    [Fact]
    public void Approve_SetsStatus()
    {
        var ec = CreateClaim();
        ec.AddExpense(DateTime.UtcNow, "Taxi", 50m);
        ec.Approve();
        ec.Status.ShouldBe(Core.DocumentStatus.Approved);
        ec.TotalSanctionedAmount.ShouldBe(50m);
    }

    [Fact]
    public void Approve_NoExpenses_Throws()
    {
        var ec = CreateClaim();
        Should.Throw<BusinessException>(() => ec.Approve());
    }

    [Fact]
    public void Submit_RequiresApproval()
    {
        var ec = CreateClaim();
        ec.AddExpense(DateTime.UtcNow, "Meal", 30m);
        Should.Throw<BusinessException>(() => ec.Submit());
    }

    [Fact]
    public void Submit_AfterApproval_Succeeds()
    {
        var ec = CreateClaim();
        ec.AddExpense(DateTime.UtcNow, "Meal", 30m);
        ec.Approve();
        ec.Submit();
        ec.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Reject_Draft_Succeeds()
    {
        var ec = CreateClaim();
        ec.AddExpense(DateTime.UtcNow, "Meal", 30m);
        ec.Reject();
        ec.Status.ShouldBe(Core.DocumentStatus.Rejected);
    }

    [Fact]
    public void Cancel_Approved_Succeeds()
    {
        var ec = CreateClaim();
        ec.AddExpense(DateTime.UtcNow, "Meal", 30m);
        ec.Approve();
        ec.Cancel();
        ec.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }
}
