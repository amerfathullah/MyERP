using System;
using System.Linq;
using MyERP.HumanResources;
using MyERP.HumanResources.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.HumanResources;

public class LoanTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _employeeId = Guid.NewGuid();

    private Loan CreateLoan(
        decimal amount = 100_000m,
        decimal rate = 6m,
        int tenure = 12,
        InterestCalculationMethod method = InterestCalculationMethod.DiminishingBalance)
    {
        return new Loan(Guid.NewGuid(), _companyId, _employeeId, "LOAN-001",
            LoanType.TermLoan, method, amount, rate, tenure);
    }

    [Fact]
    public void Loan_DefaultState()
    {
        var loan = CreateLoan();
        Assert.Equal(LoanStatus.Draft, loan.Status);
        Assert.Equal(100_000m, loan.LoanAmount);
        Assert.Equal(6m, loan.AnnualInterestRate);
        Assert.Equal(12, loan.TenureMonths);
        Assert.Equal(0m, loan.TotalAmountRepaid);
        Assert.Equal(0m, loan.TotalPrincipalRepaid);
        Assert.Equal(100_000m, loan.OutstandingBalance);
        Assert.Empty(loan.RepaymentSchedule);
    }

    [Fact]
    public void Loan_InvalidAmount_Throws()
    {
        var ex = Assert.Throws<BusinessException>(() =>
            new Loan(Guid.NewGuid(), _companyId, _employeeId, "L-1",
                LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance, 0m, 6m, 12));
        Assert.Equal("MyERP:14003", ex.Code);
    }

    [Fact]
    public void Loan_InvalidTenure_Throws()
    {
        var ex = Assert.Throws<BusinessException>(() =>
            new Loan(Guid.NewGuid(), _companyId, _employeeId, "L-1",
                LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance, 100_000m, 6m, 0));
        Assert.Equal("MyERP:14004", ex.Code);
    }

    [Fact]
    public void Loan_Sanction()
    {
        var loan = CreateLoan();
        loan.Sanction();
        Assert.Equal(LoanStatus.Sanctioned, loan.Status);
    }

    [Fact]
    public void Loan_Sanction_FromNonDraft_Throws()
    {
        var loan = CreateLoan();
        loan.Sanction();
        Assert.Throws<BusinessException>(() => loan.Sanction()); // Already sanctioned
    }

    [Fact]
    public void Loan_Disburse()
    {
        var loan = CreateLoan();
        loan.Sanction();
        loan.Disburse(new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        Assert.Equal(LoanStatus.Disbursed, loan.Status);
        Assert.Equal(new DateTime(2026, 1, 1), loan.DisbursementDate);
        Assert.Equal(new DateTime(2026, 2, 1), loan.RepaymentStartDate);
        Assert.True(loan.Emi > 0);
    }

    [Fact]
    public void Loan_Disburse_GeneratesSchedule()
    {
        var loan = CreateLoan(tenure: 12);
        loan.Sanction();
        loan.Disburse(new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        Assert.Equal(12, loan.RepaymentSchedule.Count);
        var first = loan.RepaymentSchedule.OrderBy(r => r.InstallmentNumber).First();
        Assert.Equal(1, first.InstallmentNumber);
        Assert.Equal(new DateTime(2026, 2, 1), first.PaymentDate);
        Assert.True(first.PrincipalAmount > 0 || first.InterestAmount > 0);
    }

    [Fact]
    public void Loan_EMI_DiminishingBalance()
    {
        // RM 100,000 at 6% for 12 months
        // Monthly rate = 0.5%, EMI = 100000 × 0.005 × (1.005)^12 / ((1.005)^12 - 1) ≈ 8,606.64
        var loan = CreateLoan(100_000m, 6m, 12, InterestCalculationMethod.DiminishingBalance);
        var emi = loan.CalculateEmi();

        Assert.True(emi > 8600m && emi < 8650m, $"EMI should be ~8606 but was {emi}");
    }

    [Fact]
    public void Loan_EMI_FlatRate()
    {
        // RM 100,000 at 6% for 12 months (1 year)
        // total_interest = 100000 × 6/100 × 1 = 6000
        // EMI = (100000 + 6000) / 12 = 8833.33
        var loan = CreateLoan(100_000m, 6m, 12, InterestCalculationMethod.FlatRate);
        var emi = loan.CalculateEmi();

        Assert.Equal(8833.33m, emi);
    }

    [Fact]
    public void Loan_EMI_ZeroInterest()
    {
        var loan = CreateLoan(120_000m, 0m, 12, InterestCalculationMethod.DiminishingBalance);
        var emi = loan.CalculateEmi();
        Assert.Equal(10_000m, emi); // Simple division
    }

    [Fact]
    public void Loan_Schedule_LastInstallmentAbsorbsRounding()
    {
        var loan = CreateLoan(100_000m, 6m, 12);
        loan.Sanction();
        loan.Disburse(new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        var lastEntry = loan.RepaymentSchedule.OrderBy(r => r.InstallmentNumber).Last();
        // Last installment's outstanding should be 0 (absorbs rounding)
        Assert.True(lastEntry.OutstandingAfterPayment <= 0.01m);
    }

    [Fact]
    public void Loan_Schedule_PaymentDates_Monthly()
    {
        var loan = CreateLoan(tenure: 6);
        loan.Sanction();
        loan.Disburse(new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        var dates = loan.RepaymentSchedule.OrderBy(r => r.InstallmentNumber)
            .Select(r => r.PaymentDate).ToList();

        Assert.Equal(new DateTime(2026, 2, 1), dates[0]);
        Assert.Equal(new DateTime(2026, 3, 1), dates[1]);
        Assert.Equal(new DateTime(2026, 7, 1), dates[5]);
    }

    [Fact]
    public void Loan_RecordRepayment()
    {
        var loan = CreateLoan(100_000m, 6m, 12);
        loan.Sanction();
        loan.Disburse(new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        loan.RecordRepayment(8100m, 500m);

        Assert.Equal(8100m, loan.TotalPrincipalRepaid);
        Assert.Equal(500m, loan.TotalInterestCharged);
        Assert.Equal(8600m, loan.TotalAmountRepaid);
        Assert.Equal(91_900m, loan.OutstandingBalance);
        Assert.Equal(LoanStatus.PartiallyRepaid, loan.Status);
    }

    [Fact]
    public void Loan_RecordRepayment_FullyRepaid()
    {
        var loan = CreateLoan(10_000m, 0m, 2);
        loan.Sanction();
        loan.Disburse(new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        loan.RecordRepayment(5_000m, 0m);
        Assert.Equal(LoanStatus.PartiallyRepaid, loan.Status);

        loan.RecordRepayment(5_000m, 0m);
        Assert.Equal(LoanStatus.FullyRepaid, loan.Status);
        Assert.Equal(0m, loan.OutstandingBalance);
    }

    [Fact]
    public void Loan_Penalty_Calculation()
    {
        var loan = CreateLoan();
        loan.PenaltyRate = 12m; // 12% annual penalty

        // RM 50,000 overdue for 30 days: 50000 × 12/100 × 30/365 ≈ 493.15
        var penalty = loan.CalculatePenalty(50_000m, 30);
        Assert.True(penalty > 490m && penalty < 500m);
    }

    [Fact]
    public void Loan_Penalty_ZeroRate_ReturnsZero()
    {
        var loan = CreateLoan();
        loan.PenaltyRate = 0m;
        Assert.Equal(0m, loan.CalculatePenalty(50_000m, 30));
    }

    [Fact]
    public void Loan_Penalty_ZeroDays_ReturnsZero()
    {
        var loan = CreateLoan();
        loan.PenaltyRate = 12m;
        Assert.Equal(0m, loan.CalculatePenalty(50_000m, 0));
    }

    [Fact]
    public void Loan_DemandLoan_NoSchedule()
    {
        var loan = new Loan(Guid.NewGuid(), _companyId, _employeeId, "DL-001",
            LoanType.DemandLoan, InterestCalculationMethod.DiminishingBalance,
            50_000m, 8m, 24);

        loan.Sanction();
        loan.Disburse(new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        Assert.Empty(loan.RepaymentSchedule); // Demand loans have no fixed schedule
    }

    [Fact]
    public void Loan_GracePeriod_InterestOnly()
    {
        var loan = CreateLoan(100_000m, 12m, 6);
        loan.GracePeriodMonths = 2; // First 2 months interest-only
        loan.Sanction();
        loan.Disburse(new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        var entries = loan.RepaymentSchedule.OrderBy(r => r.InstallmentNumber).ToList();

        // First 2 months: principal = 0
        Assert.Equal(0m, entries[0].PrincipalAmount);
        Assert.Equal(0m, entries[1].PrincipalAmount);
        Assert.True(entries[0].InterestAmount > 0);

        // Month 3 onwards: principal > 0
        Assert.True(entries[2].PrincipalAmount > 0);
    }
}
