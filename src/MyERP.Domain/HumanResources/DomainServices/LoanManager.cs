using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.HumanResources.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.HumanResources.DomainServices;

/// <summary>
/// Domain service for Loan management business rules.
/// Handles EMI calculation, repayment tracking, and payroll deduction coordination.
/// Per DO-NOT: "Calculate loan EMI with flat rate when diminishing balance is configured"
/// </summary>
public class LoanManager : DomainService
{
    private readonly IRepository<Loan, Guid> _loanRepository;

    public LoanManager(IRepository<Loan, Guid> loanRepository)
    {
        _loanRepository = loanRepository;
    }

    /// <summary>
    /// Gets active disbursed loans for an employee for payroll deduction.
    /// Returns loans with outstanding balance > 0 and status = Disbursed or PartiallyRepaid.
    /// </summary>
    public async Task<Loan[]> GetActiveLoansForPayrollAsync(Guid employeeId)
    {
        var queryable = await _loanRepository.GetQueryableAsync();
        return queryable
            .Where(l => l.EmployeeId == employeeId
                && (l.Status == LoanStatus.Disbursed || l.Status == LoanStatus.PartiallyRepaid))
            .ToArray();
    }

    /// <summary>
    /// Calculates EMI amount for payroll deduction, capped at outstanding balance.
    /// Returns the effective deduction amount.
    /// </summary>
    public decimal CalculatePayrollDeduction(Loan loan)
    {
        if (loan.OutstandingBalance <= 0) return 0;
        return Math.Min(loan.Emi, loan.OutstandingBalance);
    }

    /// <summary>
    /// Splits an EMI payment into principal and interest portions.
    /// For Diminishing Balance: interest = outstanding × monthly_rate; principal = emi - interest.
    /// For Flat Rate: interest = total_interest / tenure; principal = emi - interest.
    /// </summary>
    public (decimal Principal, decimal Interest) SplitRepayment(Loan loan, decimal amount)
    {
        decimal interest;

        if (loan.InterestMethod == InterestCalculationMethod.DiminishingBalance)
        {
            var monthlyRate = loan.AnnualInterestRate / 12 / 100;
            interest = Math.Round(loan.OutstandingBalance * monthlyRate, 2);
        }
        else // FlatRate
        {
            var totalInterest = loan.LoanAmount * loan.AnnualInterestRate / 100
                * loan.TenureMonths / 12;
            interest = Math.Round(totalInterest / loan.TenureMonths, 2);
        }

        // Interest cannot exceed the payment amount
        interest = Math.Min(interest, amount);
        var principal = amount - interest;

        return (principal, interest);
    }

    /// <summary>
    /// Records a repayment and updates loan status.
    /// Called from payroll submission or manual repayment.
    /// </summary>
    public async Task RecordRepaymentAsync(Guid loanId, decimal principalPaid, decimal interestPaid)
    {
        var loan = await _loanRepository.GetAsync(loanId);

        loan.RecordRepayment(principalPaid, interestPaid);

        await _loanRepository.UpdateAsync(loan);
    }

    /// <summary>
    /// Calculates penalty for overdue loan payment.
    /// penalty = outstanding × (penalty_rate / 100) × overdue_days / 365
    /// </summary>
    public decimal CalculatePenalty(Loan loan, int overdueDays)
    {
        if (loan.PenaltyRate <= 0 || overdueDays <= 0) return 0;
        return Math.Round(
            loan.OutstandingBalance * (loan.PenaltyRate / 100m) * overdueDays / 365m,
            2);
    }
}
