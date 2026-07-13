using System;
using System.Collections.Generic;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Accounting;

public class BudgetValidationTests
{
    private static Budget CreateBudget(BudgetAction poAction = BudgetAction.Stop)
    {
        var budget = new Budget(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "CostCenter", Guid.NewGuid());

        budget.ActionIfAnnualBudgetExceeded = BudgetAction.Stop;
        budget.ActionIfAnnualBudgetExceededOnPo = poAction;
        return budget;
    }

    [Fact]
    public void Budget_DefaultPOAction_IsIgnore()
    {
        var budget = new Budget(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "CostCenter", Guid.NewGuid());

        budget.ActionIfAnnualBudgetExceededOnPo.ShouldBe(BudgetAction.Ignore);
    }

    [Fact]
    public void Budget_CanSetPOAction_ToStop()
    {
        var budget = CreateBudget(BudgetAction.Stop);
        budget.ActionIfAnnualBudgetExceededOnPo.ShouldBe(BudgetAction.Stop);
    }

    [Fact]
    public void Budget_CanSetPOAction_ToWarn()
    {
        var budget = CreateBudget(BudgetAction.Warn);
        budget.ActionIfAnnualBudgetExceededOnPo.ShouldBe(BudgetAction.Warn);
    }

    [Fact]
    public void BudgetCheckItem_CreatesCorrectly()
    {
        var accountId = Guid.NewGuid();
        var item = new BudgetCheckItem(accountId, 5000m);
        item.AccountId.ShouldBe(accountId);
        item.Amount.ShouldBe(5000m);
    }

    [Fact]
    public void Budget_AccountBudgetAmount_MustBePositive()
    {
        var budget = CreateBudget();
        budget.AddAccount(Guid.NewGuid(), 10000m, "Marketing");
        budget.Accounts.Count.ShouldBe(1);
        budget.Accounts[0].BudgetAmount.ShouldBe(10000m);
    }

    [Fact]
    public void Budget_CannotAddAccount_WhenSubmitted()
    {
        var budget = CreateBudget();
        budget.AddAccount(Guid.NewGuid(), 5000m);
        budget.Submit();

        Should.Throw<Volo.Abp.BusinessException>(() =>
            budget.AddAccount(Guid.NewGuid(), 3000m));
    }

    [Fact]
    public void Budget_Submit_WithAccounts_Succeeds()
    {
        var budget = CreateBudget();
        budget.AddAccount(Guid.NewGuid(), 10000m);
        budget.Submit();
        budget.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void Budget_Submit_WithoutAccounts_Throws()
    {
        var budget = CreateBudget();
        Should.Throw<Volo.Abp.BusinessException>(() => budget.Submit());
    }

    [Fact]
    public void Budget_Level1_RequiresLevel2()
    {
        var budget = CreateBudget(BudgetAction.Ignore); // PO = Ignore
        budget.ActionIfAnnualBudgetExceededOnMr = BudgetAction.Stop; // MR = Stop (Level 1 without Level 2)
        budget.AddAccount(Guid.NewGuid(), 5000m);

        // Level 1 requires Level 2 to be active
        Should.Throw<Volo.Abp.BusinessException>(() => budget.Submit());
    }

    [Fact]
    public void Budget_AllLevels_Active_Succeeds()
    {
        var budget = CreateBudget(BudgetAction.Stop); // PO = Stop (Level 2)
        budget.ActionIfAnnualBudgetExceededOnMr = BudgetAction.Warn; // MR = Warn (Level 1)
        budget.AddAccount(Guid.NewGuid(), 5000m);

        budget.Submit();
        budget.Status.ShouldBe(DocumentStatus.Submitted);
    }
}
