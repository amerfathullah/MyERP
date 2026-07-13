using System;
using MyERP.Accounting.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Accounting;

public class BudgetTests
{
    private static Budget CreateBudget()
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CostCenter", Guid.NewGuid());

    [Fact]
    public void Create_SetsProperties()
    {
        var budget = CreateBudget();
        budget.Status.ShouldBe(Core.DocumentStatus.Draft);
        budget.Accounts.ShouldBeEmpty();
        budget.ActionIfAnnualBudgetExceeded.ShouldBe(BudgetAction.Stop);
    }

    [Fact]
    public void AddAccount_AddsLine()
    {
        var budget = CreateBudget();
        budget.AddAccount(Guid.NewGuid(), 50000m, "Marketing Expense");
        budget.Accounts.Count.ShouldBe(1);
        budget.Accounts[0].BudgetAmount.ShouldBe(50000m);
    }

    [Fact]
    public void AddAccount_RejectsZeroAmount()
    {
        var budget = CreateBudget();
        Should.Throw<ArgumentException>(() => budget.AddAccount(Guid.NewGuid(), 0));
    }

    [Fact]
    public void Submit_WithAccounts_Succeeds()
    {
        var budget = CreateBudget();
        budget.AddAccount(Guid.NewGuid(), 10000m);
        budget.Submit();
        budget.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutAccounts_Throws()
    {
        var budget = CreateBudget();
        Should.Throw<BusinessException>(() => budget.Submit());
    }

    [Fact]
    public void Submit_Level1WithoutLevel2_Throws()
    {
        var budget = CreateBudget();
        budget.AddAccount(Guid.NewGuid(), 10000m);
        budget.ActionIfAnnualBudgetExceededOnMr = BudgetAction.Stop;
        // Level 2 (PO) is still Ignore → violation
        Should.Throw<BusinessException>(() => budget.Submit())
            .Code.ShouldBe(MyERPDomainErrorCodes.BudgetLevel1RequiresLevel2);
    }

    [Fact]
    public void Cancel_SubmittedBudget_Succeeds()
    {
        var budget = CreateBudget();
        budget.AddAccount(Guid.NewGuid(), 10000m);
        budget.Submit();
        budget.Cancel();
        budget.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_DraftBudget_Throws()
    {
        var budget = CreateBudget();
        Should.Throw<BusinessException>(() => budget.Cancel());
    }

    [Fact]
    public void AddAccount_AfterSubmit_Throws()
    {
        var budget = CreateBudget();
        budget.AddAccount(Guid.NewGuid(), 10000m);
        budget.Submit();
        Should.Throw<BusinessException>(() => budget.AddAccount(Guid.NewGuid(), 5000m));
    }
}
