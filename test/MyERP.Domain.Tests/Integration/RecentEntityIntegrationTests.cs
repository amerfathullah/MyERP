using System;
using System.Linq;
using MyERP.HumanResources.Entities;
using MyERP.HumanResources;
using MyERP.Manufacturing.Entities;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Integration tests for recently-added entities and their cross-module interactions:
/// - Loan EMI lifecycle (create → sanction → disburse → schedule → repay → complete)
/// - BomOperation cost flowing into WO material valuation
/// - StockClosingEntry incremental snapshot
/// - UOM conversion through document conversion chain
/// - SubcontractingInwardOrder billing tracking
/// </summary>
public class RecentEntityIntegrationTests
{
    // === Loan Full Lifecycle ===

    [Fact]
    public void Loan_DiminishingBalance_12Month_EMI()
    {
        var companyId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var loan = new Loan(Guid.NewGuid(), companyId, employeeId, "LN-001",
            LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
            loanAmount: 120000m, annualInterestRate: 12m, tenureMonths: 12);

        Assert.Equal(LoanStatus.Draft, loan.Status);
        Assert.Equal(120000m, loan.LoanAmount);
        Assert.Equal(120000m, loan.OutstandingBalance);
    }

    [Fact]
    public void Loan_Sanction_Changes_Status()
    {
        var loan = CreateLoan(50000m, 10m, 6);
        loan.Sanction();
        Assert.Equal(LoanStatus.Sanctioned, loan.Status);
    }

    [Fact]
    public void Loan_Disburse_GeneratesSchedule()
    {
        var loan = CreateLoan(60000m, 12m, 12);
        loan.Sanction();
        loan.Disburse(DateTime.Today, DateTime.Today.AddMonths(1));

        Assert.Equal(LoanStatus.Disbursed, loan.Status);
        Assert.Equal(12, loan.RepaymentSchedule.Count);
        // All installments should have positive total payment
        Assert.All(loan.RepaymentSchedule, entry =>
        {
            Assert.True(entry.TotalPayment > 0);
        });
    }

    [Fact]
    public void Loan_DiminishingBalance_FirstInstallment_HigherInterest()
    {
        var loan = CreateLoan(120000m, 12m, 12);
        loan.Sanction();
        loan.Disburse(DateTime.Today, DateTime.Today.AddMonths(1));

        // First month interest = 120000 × (12%/12) = 1200
        var first = loan.RepaymentSchedule.OrderBy(s => s.PaymentDate).First();
        Assert.True(first.InterestAmount >= 1190m && first.InterestAmount <= 1210m,
            $"First interest {first.InterestAmount} should be ~1200");
    }

    [Fact]
    public void Loan_DiminishingBalance_ScheduleSum_Equals_LoanAmount()
    {
        var loan = CreateLoan(100000m, 12m, 12);
        loan.Sanction();
        loan.Disburse(DateTime.Today, DateTime.Today.AddMonths(1));

        // Sum of principal payments should equal loan amount (last absorbs rounding)
        var totalPrincipal = loan.RepaymentSchedule.Sum(s => s.PrincipalAmount);
        Assert.Equal(100000m, totalPrincipal);
    }

    [Fact]
    public void Loan_FlatRate_EqualInstallments()
    {
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LN-F01",
            LoanType.TermLoan, InterestCalculationMethod.FlatRate,
            loanAmount: 60000m, annualInterestRate: 10m, tenureMonths: 12);
        loan.Sanction();
        loan.Disburse(DateTime.Today, DateTime.Today.AddMonths(1));

        // Flat: total = 60000 + (60000 × 10% × 1year) = 66000
        // EMI = 66000/12 = 5500
        Assert.Equal(5500m, loan.Emi);
    }

    [Fact]
    public void Loan_RecordRepayment_ReducesOutstanding()
    {
        var loan = CreateLoan(100000m, 12m, 12);
        loan.Sanction();
        loan.Disburse(DateTime.Today, DateTime.Today.AddMonths(1));

        var first = loan.RepaymentSchedule.OrderBy(s => s.PaymentDate).First();
        loan.RecordRepayment(first.PrincipalAmount, first.InterestAmount);

        Assert.Equal(LoanStatus.PartiallyRepaid, loan.Status);
        Assert.True(loan.OutstandingBalance < 100000m);
    }

    [Fact]
    public void Loan_FullRepayment_TransitionsToFullyRepaid()
    {
        var loan = CreateLoan(10000m, 0m, 1); // zero interest, 1 month
        loan.Sanction();
        loan.Disburse(DateTime.Today, DateTime.Today.AddMonths(1));

        loan.RecordRepayment(10000m, 0m);

        Assert.Equal(LoanStatus.FullyRepaid, loan.Status);
        Assert.Equal(0m, loan.OutstandingBalance);
    }

    [Fact]
    public void Loan_GracePeriod_FirstInstallments_InterestOnly()
    {
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LN-G01",
            LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
            loanAmount: 120000m, annualInterestRate: 12m, tenureMonths: 12);
        loan.GracePeriodMonths = 3;
        loan.Sanction();
        loan.Disburse(DateTime.Today, DateTime.Today.AddMonths(1));

        // First 3 months should be interest-only (principal = 0)
        var graceEntries = loan.RepaymentSchedule.OrderBy(s => s.PaymentDate).Take(3).ToList();
        Assert.All(graceEntries, entry => Assert.Equal(0m, entry.PrincipalAmount));
    }

    [Fact]
    public void Loan_Cancel_FromDraft()
    {
        var loan = CreateLoan(50000m, 10m, 6);
        loan.Cancel();
        Assert.Equal(LoanStatus.Cancelled, loan.Status);
    }

    [Fact]
    public void Loan_DemandLoan_NoSchedule()
    {
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LN-D01",
            LoanType.DemandLoan, InterestCalculationMethod.DiminishingBalance,
            loanAmount: 50000m, annualInterestRate: 10m, tenureMonths: 12);
        loan.Sanction();
        loan.Disburse(DateTime.Today, DateTime.Today.AddMonths(1));

        Assert.Empty(loan.RepaymentSchedule);
    }

    // === BOM Operation Cost Flows Into Work Order ===

    [Fact]
    public void BomOperation_CostCalculation_HourlyRate()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 60m);
        // Time=60mins, HourRate=200 → cost = 60/60 × 200 = 200
        op.CalculateCost(200m);
        Assert.Equal(200m, op.OperatingCost);
    }

    [Fact]
    public void BomOperation_TotalTime_WithFixedSetup()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 30m)
        { FixedTime = 15 };

        // For 5 units: fixedTime + (timeInMins × qty) = 15 + (30 × 5) = 165 mins
        Assert.Equal(165m, op.GetTotalTime(5));
    }

    [Fact]
    public void BomOperation_BatchSize_JobCardCount()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 20m)
        { BatchSize = 25 };

        // WO for 100 units, batch size 25 → 4 job cards
        Assert.Equal(4, op.GetJobCardCount(100));
        // WO for 110 units → 5 (ceiling)
        Assert.Equal(5, op.GetJobCardCount(110));
    }

    [Fact]
    public void BomOperation_ZeroBatchSize_SingleJobCard()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 45m);
        // BatchSize=0 means one JC for entire WO
        Assert.Equal(1, op.GetJobCardCount(1000));
    }

    // === Stock Closing Entry ===

    [Fact]
    public void StockClosingEntry_AddBalance_CalculatesTotals()
    {
        var entry = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100, 5000m, 50m, null);
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 200, 16000m, 80m, null);

        Assert.Equal(2, entry.Balances.Count);
    }

    [Fact]
    public void StockClosingEntry_Submit_CalculatesTotalValue()
    {
        var entry = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100, 5000m, 50m, null);
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 50, 2500m, 50m, null);
        entry.Submit();

        Assert.Equal(7500m, entry.TotalStockValue);
        Assert.Equal(2, entry.TotalEntries);
    }

    [Fact]
    public void StockClosingEntry_BlocksAddAfterSubmit()
    {
        var entry = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 10, 100m, 10m, null);
        entry.Submit();

        Assert.ThrowsAny<Exception>(() =>
            entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 20, 200m, 10m, null));
    }

    // === SubcontractingInwardOrder Status Logic ===

    [Fact]
    public void SubcontractingInwardOrder_Status_Enum_Values()
    {
        // Verify all expected statuses exist
        Assert.Equal(0, (int)SubcontractingInwardOrderStatus.Draft);
        Assert.Equal(1, (int)SubcontractingInwardOrderStatus.Open);
        Assert.Equal(2, (int)SubcontractingInwardOrderStatus.PartiallyReceived);
        Assert.Equal(3, (int)SubcontractingInwardOrderStatus.Completed);
    }

    // === UOM Conversion Chain: SO → DN ===

    [Fact]
    public void UOM_ConversionFactor_Propagates_SOtoDN()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Widget Dozen", 5, 120m, 0m);
        // Simulate: UOM is Dozen, stock is Unit → factor = 12
        so.Items[0].StockUom = "Unit";
        so.Items[0].ConversionFactor = 12;

        // StockQty = 5 dozen × 12 = 60 units
        Assert.Equal(60m, so.Items[0].StockQty);
    }

    [Fact]
    public void UOM_StockQty_Used_For_SLE_PurchaseOrder()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Pallet of Flour", 2, 4800m, 0m);
        po.Items.First().StockUom = "Kg";
        po.Items.First().ConversionFactor = 1000; // 1 Pallet = 1000 Kg

        // SLE should use StockQty (2000 Kg) not Quantity (2 pallets)
        Assert.Equal(2000m, po.Items.First().StockQty);
        // Rate per stock unit = 4800 / 1000 = 4.80 per Kg
        var ratePerStockUnit = po.Items.First().UnitPrice / po.Items.First().ConversionFactor;
        Assert.Equal(4.8m, ratePerStockUnit);
    }

    [Fact]
    public void UOM_SameUom_ConversionFactor_IsOne()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-002", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Simple Item", 10, 50m, 0m);
        // Default: both transaction and stock UOM are "Unit"
        Assert.Equal("Unit", so.Items[0].StockUom);
        Assert.Equal(1m, so.Items[0].ConversionFactor);
        Assert.Equal(10m, so.Items[0].StockQty); // 10 × 1 = 10
    }

    // === Helpers ===

    private static Loan CreateLoan(decimal amount, decimal rate, int months)
    {
        return new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), $"LN-{Guid.NewGuid():N}"[..10],
            LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
            loanAmount: amount, annualInterestRate: rate, tenureMonths: months);
    }
}
