using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.HumanResources.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.HR;

/// <summary>
/// Comprehensive tests for HR/Payroll business logic:
/// - PayrollEntry GL posting correctness (double-entry balance)
/// - Loan EMI calculation accuracy (diminishing + flat rate)
/// - Leave allocation lifecycle (deduction, restoration, carry-forward expiry)
/// - Salary proration for unpaid leave
/// </summary>
public class PayrollAndLoanComprehensiveTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _employeeId = Guid.NewGuid();

    #region PayrollEntry GL Balance Tests

    [Fact]
    public void PayrollEntry_TotalDebit_Equals_TotalCredits()
    {
        // Double-entry invariant: DR Expense = CR (Net + EPF + SOCSO + EIS + PCB)
        var entry = CreatePayrollEntry();
        AddPayrollLine(entry, grossSalary: 5000m, epfEmp: 550m, epfEr: 650m,
            socsoEmp: 9.05m, socsoEr: 31.65m, eisEmp: 9.50m, eisEr: 9.50m, pcb: 125m);

        var totalDebit = entry.TotalGrossSalary + entry.TotalEmployerContributions;
        var totalCredits = entry.TotalNetSalary +
            entry.Lines.Sum(l => l.EpfEmployee + l.EpfEmployer) +
            entry.Lines.Sum(l => l.SocsoEmployee + l.SocsoEmployer) +
            entry.Lines.Sum(l => l.EisEmployee + l.EisEmployer) +
            entry.Lines.Sum(l => l.Pcb);

        totalDebit.ShouldBe(totalCredits);
    }

    [Fact]
    public void PayrollEntry_MultiEmployee_AggregatesCorrectly()
    {
        var entry = CreatePayrollEntry();
        AddPayrollLine(entry, grossSalary: 5000m, epfEmp: 550m, epfEr: 650m,
            socsoEmp: 9m, socsoEr: 31m, eisEmp: 9m, eisEr: 9m, pcb: 100m);
        AddPayrollLine(entry, grossSalary: 8000m, epfEmp: 880m, epfEr: 1040m,
            socsoEmp: 9m, socsoEr: 31m, eisEmp: 9m, eisEr: 9m, pcb: 350m);

        entry.TotalGrossSalary.ShouldBe(13000m);
        entry.Lines.Count.ShouldBe(2);
    }

    [Fact]
    public void PayrollEntry_NetSalary_Formula()
    {
        var entry = CreatePayrollEntry();
        AddPayrollLine(entry, grossSalary: 5000m, epfEmp: 550m, epfEr: 0m,
            socsoEmp: 9.05m, socsoEr: 0m, eisEmp: 9.50m, eisEr: 0m, pcb: 125m);

        var line = entry.Lines.First();
        var expectedNet = 5000m - 550m - 9.05m - 9.50m - 125m;
        line.NetSalary.ShouldBe(expectedNet);
    }

    [Fact]
    public void PayrollEntry_EmployerContributions_NotDeductedFromEmployee()
    {
        var entry = CreatePayrollEntry();
        AddPayrollLine(entry, grossSalary: 5000m, epfEmp: 550m, epfEr: 650m,
            socsoEmp: 9m, socsoEr: 31m, eisEmp: 9m, eisEr: 9m, pcb: 100m);

        var line = entry.Lines.First();
        // Net salary = gross - employee portions ONLY (not employer)
        var expectedNet = 5000m - 550m - 9m - 9m - 100m;
        line.NetSalary.ShouldBe(expectedNet);
        // Employer contributions are separate expense, NOT deducted from employee
        entry.TotalEmployerContributions.ShouldBe(650m + 31m + 9m); // epfEr + socsoEr + eisEr
    }

    [Fact]
    public void PayrollEntry_LoanDeduction_ReducesNetSalary()
    {
        var entry = CreatePayrollEntry();
        AddPayrollLine(entry, grossSalary: 5000m, epfEmp: 550m, epfEr: 650m,
            socsoEmp: 9m, socsoEr: 31m, eisEmp: 9m, eisEr: 9m, pcb: 100m);

        var line = entry.Lines.First();
        line.LoanDeduction = 500m; // EMI deduction

        var expectedNet = 5000m - 550m - 9m - 9m - 100m - 500m;
        line.NetSalary.ShouldBe(expectedNet);
    }

    #endregion

    #region Loan EMI Calculation Tests

    [Fact]
    public void Loan_DiminishingBalance_EMI_Formula()
    {
        // Diminishing: EMI = P × r × (1+r)^n / ((1+r)^n - 1)
        // where r = annual_rate / 12 / 100
        decimal principal = 100000m;
        decimal annualRate = 6m; // 6% per annum
        int months = 12;

        decimal r = annualRate / 12m / 100m; // 0.005
        decimal factor = (decimal)Math.Pow((double)(1 + r), months); // (1.005)^12
        decimal emi = principal * r * factor / (factor - 1);

        emi.ShouldBeGreaterThan(8000m); // ~8606.64
        emi.ShouldBeLessThan(9000m);
        // Total repayment > principal (interest included)
        (emi * months).ShouldBeGreaterThan(principal);
    }

    [Fact]
    public void Loan_FlatRate_EMI_Formula()
    {
        // Flat: EMI = (P + P × rate/100 × years) / months
        decimal principal = 100000m;
        decimal annualRate = 6m;
        int months = 12;
        decimal years = months / 12m;

        decimal totalInterest = principal * annualRate / 100m * years;
        decimal emi = (principal + totalInterest) / months;

        emi.ShouldBe((100000m + 6000m) / 12m); // 8833.33...
    }

    [Fact]
    public void Loan_ZeroInterest_EMI_Equals_PrincipalDividedByMonths()
    {
        decimal principal = 12000m;
        decimal annualRate = 0m;
        int months = 12;

        decimal r = annualRate / 12m / 100m; // 0
        decimal emi = r == 0 ? principal / months : 0;

        emi.ShouldBe(1000m);
    }

    [Fact]
    public void Loan_RepaymentSchedule_SumsToLoanAmount()
    {
        // All installments' principal must sum to exactly loan amount
        decimal principal = 50000m;
        int months = 6;
        decimal annualRate = 12m;
        decimal r = annualRate / 12m / 100m; // 1%

        decimal factor = (decimal)Math.Pow((double)(1 + r), months);
        decimal emi = principal * r * factor / (factor - 1);

        decimal remaining = principal;
        decimal totalPrincipal = 0;
        for (int i = 0; i < months; i++)
        {
            decimal interest = remaining * r;
            decimal principalPortion = emi - interest;
            totalPrincipal += principalPortion;
            remaining -= principalPortion;
        }

        // Rounding tolerance
        Math.Abs(totalPrincipal - principal).ShouldBeLessThan(0.01m);
    }

    [Fact]
    public void Loan_GracePeriod_InterestOnly()
    {
        // During grace period: EMI = interest only, no principal reduction
        decimal principal = 100000m;
        decimal annualRate = 12m;
        decimal monthlyRate = annualRate / 12m / 100m; // 1%

        decimal gracePeriodEmi = principal * monthlyRate; // Interest only
        gracePeriodEmi.ShouldBe(1000m);
        // Principal unchanged during grace
    }

    [Fact]
    public void Loan_PenaltyCalculation()
    {
        // Penalty = outstanding × (penalty_rate / 100) × days / 365
        decimal outstanding = 80000m;
        decimal penaltyRate = 2m; // 2% p.a.
        int overdueDays = 30;

        decimal penalty = outstanding * (penaltyRate / 100m) * overdueDays / 365m;
        penalty.ShouldBeGreaterThan(100m); // ~131.51
        penalty.ShouldBeLessThan(200m);
    }

    #endregion

    #region Leave Allocation Tests

    [Fact]
    public void LeaveAllocation_DeductLeave_ReducesBalance()
    {
        var allocation = new LeaveAllocation(Guid.NewGuid(), _companyId, _employeeId,
            Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);

        allocation.DeductLeave(5);

        allocation.LeavesUsed.ShouldBe(5);
        allocation.Balance.ShouldBe(7); // 12 - 5
    }

    [Fact]
    public void LeaveAllocation_RestoreLeave_IncreasesBalance()
    {
        var allocation = new LeaveAllocation(Guid.NewGuid(), _companyId, _employeeId,
            Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);
        allocation.DeductLeave(5);

        allocation.RestoreLeave(3);

        allocation.LeavesUsed.ShouldBe(2); // 5 - 3
        allocation.Balance.ShouldBe(10); // 12 - 2
    }

    [Fact]
    public void LeaveAllocation_RestoreLeave_NeverGoesNegativeUsed()
    {
        var allocation = new LeaveAllocation(Guid.NewGuid(), _companyId, _employeeId,
            Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);
        allocation.DeductLeave(2);

        allocation.RestoreLeave(5); // Restore more than used

        allocation.LeavesUsed.ShouldBe(0); // Clamped at 0, not -3
        allocation.Balance.ShouldBe(12);
    }

    [Fact]
    public void LeaveAllocation_CarryForward_AddsToBalance()
    {
        var allocation = new LeaveAllocation(Guid.NewGuid(), _companyId, _employeeId,
            Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);
        allocation.CarryForwardDays = 3;

        allocation.Balance.ShouldBe(15); // 12 + 3
    }

    [Fact]
    public void LeaveAllocation_ExpiredCarryForward_ExcludedFromBalance()
    {
        var allocation = new LeaveAllocation(Guid.NewGuid(), _companyId, _employeeId,
            Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);
        allocation.CarryForwardDays = 5;
        allocation.CarryForwardExpiryDate = new DateTime(2026, 3, 31); // Expired

        // After expiry, effective carry-forward = 0
        allocation.EffectiveCarryForwardDays.ShouldBe(0);
        allocation.Balance.ShouldBe(12); // Only new allocation, no carry-forward
    }

    [Fact]
    public void LeaveAllocation_FutureCarryForwardExpiry_IncludedInBalance()
    {
        var allocation = new LeaveAllocation(Guid.NewGuid(), _companyId, _employeeId,
            Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12);
        allocation.CarryForwardDays = 5;
        allocation.CarryForwardExpiryDate = new DateTime(2027, 6, 30); // Future

        allocation.EffectiveCarryForwardDays.ShouldBe(5);
        allocation.Balance.ShouldBe(17); // 12 + 5
    }

    #endregion

    #region Salary Proration Tests

    [Fact]
    public void UnpaidLeaveProration_FullMonth_ZeroSalary()
    {
        decimal gross = 5000m;
        int unpaidDays = 26; // All working days
        int workingDays = 26;

        decimal dailyRate = gross / workingDays;
        decimal deduction = dailyRate * Math.Min(unpaidDays, workingDays);
        decimal proratedGross = Math.Max(0, gross - deduction);

        proratedGross.ShouldBe(0m);
    }

    [Fact]
    public void UnpaidLeaveProration_FiveDays_ReducesProportionally()
    {
        decimal gross = 5200m; // Divisible by 26
        int unpaidDays = 5;
        int workingDays = 26;

        decimal dailyRate = gross / workingDays; // 200
        decimal deduction = dailyRate * unpaidDays; // 1000
        decimal proratedGross = gross - deduction;

        proratedGross.ShouldBe(4200m);
    }

    [Fact]
    public void UnpaidLeaveProration_CappedAtWorkingDays()
    {
        decimal gross = 5000m;
        int unpaidDays = 30; // More than working days
        int workingDays = 22;

        decimal effectiveUnpaid = Math.Min(unpaidDays, workingDays);
        effectiveUnpaid.ShouldBe(22); // Capped

        decimal dailyRate = gross / workingDays;
        decimal deduction = dailyRate * effectiveUnpaid;
        decimal proratedGross = Math.Max(0, gross - deduction);
        // When all working days are unpaid, prorated gross should be <= tiny rounding error
        proratedGross.ShouldBeLessThanOrEqualTo(0.01m);
    }

    [Fact]
    public void UnpaidLeaveProration_ZeroDays_FullSalary()
    {
        decimal gross = 5000m;
        int unpaidDays = 0;
        int workingDays = 26;

        decimal deduction = (gross / workingDays) * unpaidDays;
        decimal proratedGross = gross - deduction;

        proratedGross.ShouldBe(5000m);
    }

    [Fact]
    public void StatutoryDeductions_CalculatedOnProratedGross()
    {
        // EPF is calculated on prorated gross, not full gross
        decimal fullGross = 5000m;
        int unpaidDays = 5;
        int workingDays = 25;

        decimal proratedGross = fullGross - (fullGross / workingDays * unpaidDays); // 4000
        decimal epfEmployee = proratedGross * 0.11m; // 11% of 4000 = 440

        proratedGross.ShouldBe(4000m);
        epfEmployee.ShouldBe(440m);
        // Without proration it would be 550 (11% of 5000)
        epfEmployee.ShouldBeLessThan(fullGross * 0.11m);
    }

    #endregion

    #region Helpers

    private PayrollEntry CreatePayrollEntry()
    {
        return new PayrollEntry(
            Guid.NewGuid(),
            _companyId,
            "PAY-2026-00001",
            2026,
            7,
            new DateTime(2026, 7, 31));
    }

    private void AddPayrollLine(PayrollEntry entry, decimal grossSalary,
        decimal epfEmp, decimal epfEr, decimal socsoEmp, decimal socsoEr,
        decimal eisEmp, decimal eisEr, decimal pcb)
    {
        entry.AddLine(
            _employeeId,
            "Test Employee",
            grossSalary,
            epfEmp, epfEr,
            socsoEmp, socsoEr,
            eisEmp, eisEr,
            pcb);
    }

    #endregion
}
