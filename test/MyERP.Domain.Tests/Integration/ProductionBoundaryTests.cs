using System;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.HumanResources;
using MyERP.HumanResources.Entities;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Projects;
using MyERP.Projects.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;
using DocumentStatus = MyERP.Core.DocumentStatus;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Boundary condition tests for critical production scenarios.
/// These verify that entities handle edge cases without runtime exceptions.
/// </summary>
public class ProductionBoundaryTests
{
    // === Loan edge cases ===

    [Fact]
    public void Loan_ZeroInterestRate_ValidEMI()
    {
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LN-ZERO",
            LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
            loanAmount: 60000m, annualInterestRate: 0m, tenureMonths: 12);
        loan.Sanction();
        loan.Disburse(DateTime.Today, DateTime.Today.AddMonths(1));

        // EMI with 0% interest = principal / months
        Assert.Equal(5000m, loan.Emi);
        Assert.Equal(12, loan.RepaymentSchedule.Count);
    }

    [Fact]
    public void Loan_Penalty_ZeroDays_ReturnsZero()
    {
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LN-PEN",
            LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
            loanAmount: 100000m, annualInterestRate: 12m, tenureMonths: 12);
        loan.PenaltyRate = 18m;

        Assert.Equal(0m, loan.CalculatePenalty(50000m, 0));
    }

    [Fact]
    public void Loan_Penalty_ZeroRate_ReturnsZero()
    {
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LN-PEN2",
            LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
            loanAmount: 100000m, annualInterestRate: 12m, tenureMonths: 12);
        loan.PenaltyRate = 0m;

        Assert.Equal(0m, loan.CalculatePenalty(50000m, 30));
    }

    [Fact]
    public void Loan_InvalidAmount_Throws()
    {
        Assert.Throws<BusinessException>(() =>
            new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LN-BAD",
                LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
                loanAmount: 0m, annualInterestRate: 12m, tenureMonths: 12));
    }

    [Fact]
    public void Loan_InvalidTenure_Throws()
    {
        Assert.Throws<BusinessException>(() =>
            new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LN-BAD2",
                LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
                loanAmount: 50000m, annualInterestRate: 12m, tenureMonths: 0));
    }

    // === Project percent complete edge cases ===

    [Fact]
    public void Project_SetPercentComplete_ExactBoundary0()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-B1", "Boundary Test");
        project.PercentCompleteMethod = PercentCompleteMethod.Manual;
        project.SetPercentComplete(0m);
        Assert.Equal(0m, project.PercentComplete);
    }

    [Fact]
    public void Project_SetPercentComplete_ExactBoundary100()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-B2", "Boundary Test");
        project.PercentCompleteMethod = PercentCompleteMethod.Manual;
        project.SetPercentComplete(100m);
        Assert.Equal(100m, project.PercentComplete);
    }

    [Fact]
    public void Project_SetPercentComplete_JustBelowZero_Throws()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-B3", "Boundary Test");
        project.PercentCompleteMethod = PercentCompleteMethod.Manual;
        Assert.Throws<BusinessException>(() => project.SetPercentComplete(-0.01m));
    }

    [Fact]
    public void Project_SetPercentComplete_JustAbove100_Throws()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-B4", "Boundary Test");
        project.PercentCompleteMethod = PercentCompleteMethod.Manual;
        Assert.Throws<BusinessException>(() => project.SetPercentComplete(100.01m));
    }

    // === BOM Operation edge cases ===

    [Fact]
    public void BomOperation_ZeroTimeInMins_CostIsZero()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 0m);
        op.CalculateCost(500m);
        Assert.Equal(0m, op.OperatingCost);
    }

    [Fact]
    public void BomOperation_ZeroHourRate_CostIsZero()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 60m);
        op.CalculateCost(0m);
        Assert.Equal(0m, op.OperatingCost);
    }

    [Fact]
    public void BomOperation_TotalTime_ZeroQty_OnlyFixedTime()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 30m)
        { FixedTime = 15 };
        Assert.Equal(15m, op.GetTotalTime(0));
    }

    [Fact]
    public void BomOperation_JobCardCount_ExactMultiple()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 20m)
        { BatchSize = 50 };
        // 200 / 50 = exactly 4 — no ceiling needed
        Assert.Equal(4, op.GetJobCardCount(200));
    }

    // === Sales Order edge cases ===

    [Fact]
    public void SalesOrder_Submit_SetsToDeliverAndBill()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item", 10, 100m, 0m);
        so.Submit();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);
    }

    [Fact]
    public void SalesOrder_Close_Reopen_Cycle()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-002", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item", 5, 200m, 0m);
        so.Submit();
        so.Close();
        Assert.Equal(DocumentStatus.Closed, so.Status);

        so.Reopen();
        // After reopen, should be back to fulfillment status
        Assert.NotEqual(DocumentStatus.Closed, so.Status);
    }

    [Fact]
    public void SalesOrder_UOM_StockQty_LargeConversionFactor()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-003", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Container", 2, 50000m, 0m);
        so.Items[0].StockUom = "Unit";
        so.Items[0].ConversionFactor = 5000; // 1 container = 5000 units
        Assert.Equal(10000m, so.Items[0].StockQty);
    }

    // === Account entity edge cases ===

    [Fact]
    public void Account_EmptyAccountCode_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new Account(Guid.NewGuid(), Guid.NewGuid(), "", "Test", AccountType.Asset));
    }

    [Fact]
    public void Account_EmptyAccountName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new Account(Guid.NewGuid(), Guid.NewGuid(), "1001", "", AccountType.Asset));
    }

    // === StockClosingEntry edge cases ===

    [Fact]
    public void StockClosingEntry_EmptySubmit_Throws()
    {
        var entry = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        Assert.ThrowsAny<Exception>(() => entry.Submit());
    }

    [Fact]
    public void StockClosingEntry_Cancel_AfterSubmit()
    {
        var entry = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100, 5000m, 50m, null);
        entry.Submit();
        entry.Cancel();
        // Should not throw — cancel from submitted is valid
    }
}
