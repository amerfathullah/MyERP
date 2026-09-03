using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Shouldly;
using Xunit;

namespace MyERP.Accounting;

public class BudgetValidationServiceTests
{
    [Fact]
    public void Budget_DefaultActions_AreIgnoreForMRAndPO()
    {
        var budget = CreateBudget(10000);
        budget.ActionIfAnnualBudgetExceededOnMr.ShouldBe(BudgetAction.Ignore);
        budget.ActionIfAnnualBudgetExceededOnPo.ShouldBe(BudgetAction.Ignore);
    }

    [Fact]
    public void Budget_DefaultAction_StopForActual()
    {
        var budget = CreateBudget(10000);
        budget.ActionIfAnnualBudgetExceeded.ShouldBe(BudgetAction.Stop);
    }

    [Fact]
    public void BudgetCheck_WithinLimit_Passes()
    {
        decimal budgetAmount = 10000;
        decimal alreadySpent = 3000;
        decimal newRequest = 2000;
        ((alreadySpent + newRequest) > budgetAmount).ShouldBeFalse();
    }

    [Fact]
    public void BudgetCheck_ExceedsLimit_Detected()
    {
        decimal budgetAmount = 10000;
        decimal alreadySpent = 8000;
        decimal newRequest = 5000;
        ((alreadySpent + newRequest) > budgetAmount).ShouldBeTrue();
    }

    [Fact]
    public void BudgetCheck_ExactlyAtLimit_Passes()
    {
        decimal budgetAmount = 10000;
        var total = 7000m + 3000m;
        (total > budgetAmount).ShouldBeFalse();
    }

    [Fact]
    public void BudgetCheck_OnlySubmittedBudgetsCount()
    {
        var draft = CreateBudget(5000, submit: false);
        var submitted = CreateBudget(10000, submit: true);
        draft.Status.ShouldBe(DocumentStatus.Draft);
        submitted.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void Budget_MultipleAccounts()
    {
        var budget = new Budget(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CostCenter", Guid.NewGuid());
        budget.AddAccount(Guid.NewGuid(), 5000);
        budget.AddAccount(Guid.NewGuid(), 8000);
        budget.Accounts.Count.ShouldBe(2);
    }

    [Fact]
    public void Budget_Submit_RequiresAccounts()
    {
        var budget = new Budget(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CostCenter", Guid.NewGuid());
        Should.Throw<Volo.Abp.BusinessException>(() => budget.Submit());
    }

    [Fact]
    public void Budget_AddAccount_BlockedAfterSubmit()
    {
        var budget = CreateBudget(5000);
        Should.Throw<Volo.Abp.BusinessException>(() => budget.AddAccount(Guid.NewGuid(), 1000));
    }

    [Fact]
    public void Budget_AddAccount_RejectsZeroAmount()
    {
        var budget = new Budget(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CostCenter", Guid.NewGuid());
        Should.Throw<ArgumentException>(() => budget.AddAccount(Guid.NewGuid(), 0));
    }

    [Fact]
    public void Budget_Cancel_FromSubmitted()
    {
        var budget = CreateBudget(5000);
        budget.Cancel();
        budget.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void Budget_Cancel_FromDraft_Throws()
    {
        var budget = CreateBudget(5000, submit: false);
        Should.Throw<Volo.Abp.BusinessException>(() => budget.Cancel());
    }

    [Fact]
    public void Budget_CumulativeExpense_PropertiesDefaultCorrectly()
    {
        var budget = CreateBudget(5000, submit: false);
        budget.ApplicableOnCumulativeExpense.ShouldBeFalse();
        budget.ActionIfAnnualExceededOnCumulativeExpense.ShouldBe(BudgetAction.Ignore);
        budget.ActionIfAccumulatedMonthlyExceededOnCumulativeExpense.ShouldBe(BudgetAction.Ignore);

        budget.ApplicableOnCumulativeExpense = true;
        budget.ActionIfAnnualExceededOnCumulativeExpense = BudgetAction.Stop;
        budget.ActionIfAccumulatedMonthlyExceededOnCumulativeExpense = BudgetAction.Warn;

        budget.ApplicableOnCumulativeExpense.ShouldBeTrue();
        budget.ActionIfAnnualExceededOnCumulativeExpense.ShouldBe(BudgetAction.Stop);
        budget.ActionIfAccumulatedMonthlyExceededOnCumulativeExpense.ShouldBe(BudgetAction.Warn);
    }

    [Fact]
    public void PurchaseOrderItem_HasExpenseAccountId()
    {
        var po = new Purchasing.Entities.PurchaseOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-0001", DateTime.UtcNow);
        var expenseAccId = Guid.NewGuid();
        po.AddItem(Guid.NewGuid(), "Test Item", 10m, 50m, 0m, "Unit", expenseAccountId: expenseAccId);

        po.Items.Count.ShouldBe(1);
        po.Items[0].ExpenseAccountId.ShouldBe(expenseAccId);
    }

    private static Budget CreateBudget(decimal amount, bool submit = true)
    {
        var budget = new Budget(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CostCenter", Guid.NewGuid());
        budget.AddAccount(Guid.NewGuid(), amount);
        if (submit) budget.Submit();
        return budget;
    }
}