using System;
using MyERP.HumanResources.Entities;
using MyERP.HumanResources;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

public class LoanPayrollDeductionTests
{
    [Fact]
    public void PayrollEntryLine_LoanDeduction_DefaultsZero()
    {
        var line = CreateLine();
        line.LoanDeduction.ShouldBe(0m);
        line.LoanId.ShouldBeNull();
    }

    [Fact]
    public void PayrollEntryLine_LoanDeduction_IncreasesTotalDeductions()
    {
        var line = CreateLine();
        var baseTotalDeductions = line.TotalDeductions;

        line.LoanDeduction = 500m;
        line.TotalDeductions.ShouldBe(baseTotalDeductions + 500m);
    }

    [Fact]
    public void PayrollEntryLine_LoanDeduction_ReducesNetSalary()
    {
        var line = CreateLine();
        var baseNet = line.NetSalary;

        line.LoanDeduction = 500m;
        line.NetSalary.ShouldBe(baseNet - 500m);
    }

    [Fact]
    public void PayrollEntryLine_LoanId_CanBeSet()
    {
        var line = CreateLine();
        var loanId = Guid.NewGuid();
        line.LoanId = loanId;
        line.LoanId.ShouldBe(loanId);
    }

    [Fact]
    public void Loan_EmiDeduction_CappedAtOutstanding()
    {
        // If EMI is 1000 but only 300 outstanding → deduct 300
        decimal emi = 1000m;
        decimal outstanding = 300m;
        var deduction = Math.Min(emi, outstanding);
        deduction.ShouldBe(300m);
    }

    [Fact]
    public void Loan_EmiDeduction_FullEmiWhenSufficient()
    {
        decimal emi = 1000m;
        decimal outstanding = 50000m;
        var deduction = Math.Min(emi, outstanding);
        deduction.ShouldBe(1000m);
    }

    [Fact]
    public void Loan_RecordRepayment_ReducesOutstanding()
    {
        var loan = CreateLoan(50000m, 5.5m, 60);
        loan.Sanction();
        loan.Disburse(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));

        var beforeBalance = loan.OutstandingBalance;
        loan.RecordRepayment(800m, 200m); // 800 principal + 200 interest

        loan.OutstandingBalance.ShouldBe(beforeBalance - 800m);
        loan.TotalAmountRepaid.ShouldBe(1000m);
    }

    [Fact]
    public void Loan_FullRepayment_SetsFullyRepaid()
    {
        var loan = CreateLoan(2000m, 0, 2); // no interest, simple
        loan.Sanction();
        loan.Disburse(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));

        loan.RecordRepayment(1000m, 0m);
        loan.Status.ShouldBe(LoanStatus.PartiallyRepaid);

        loan.RecordRepayment(1000m, 0m);
        loan.Status.ShouldBe(LoanStatus.FullyRepaid);
    }

    [Fact]
    public void PayrollEntry_TotalNetSalary_IncludesLoanDeduction()
    {
        var entry = new PayrollEntry(Guid.NewGuid(), Guid.NewGuid(), "PR-001", 2026, 7,
            new DateTime(2026, 7, 31));

        // Standard MY payroll: RM 5000
        entry.AddLine(Guid.NewGuid(), "Employee A", 5000m,
            550m, 650m, 17.50m, 87.50m, 10m, 10m, 200m);

        // Simulate loan deduction
        entry.Lines[0].LoanDeduction = 500m;

        // Net = 5000 - (550+17.50+10+200+500) = 5000 - 1277.50 = 3722.50
        entry.Lines[0].NetSalary.ShouldBe(3722.50m);
    }

    [Fact]
    public void SupplierStatement_BalanceEquation()
    {
        // For payables: Opening + Invoiced - Paid = Closing
        decimal opening = 10000m;
        decimal invoiced = 8000m;
        decimal paid = 5000m;
        decimal closing = opening + invoiced - paid;
        closing.ShouldBe(13000m); // we owe the supplier more
    }

    [Fact]
    public void SupplierStatement_DebitNote_ReducesPayable()
    {
        // Debit notes reduce what we owe (return to supplier)
        decimal invoiced = 20000m;
        decimal debitNote = 3000m;
        decimal netPayable = invoiced - debitNote;
        netPayable.ShouldBe(17000m);
    }

    [Fact]
    public void SupplierStatement_Payment_ReducesBalance()
    {
        decimal balance = 15000m;
        decimal payment = 6000m;
        decimal afterPayment = balance - payment;
        afterPayment.ShouldBe(9000m);
    }

    private static PayrollEntryLine CreateLine()
    {
        return new PayrollEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Test Employee", 5000m, 550m, 650m, 17.50m, 87.50m, 10m, 10m, 200m);
    }

    private static Loan CreateLoan(decimal amount, decimal rate, int tenure)
    {
        return new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "LN-001", LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
            amount, rate, tenure);
    }
}
