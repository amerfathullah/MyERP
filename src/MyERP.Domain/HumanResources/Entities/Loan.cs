using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Employee Loan — tracks loan disbursement, repayment schedule, and outstanding balance.
/// Supports both Diminishing Balance (EMI) and Flat Rate interest methods.
/// 
/// Per ERPNext:
/// - Diminishing: EMI = P × r × (1+r)^n / ((1+r)^n - 1)
/// - Flat: total_interest = principal × rate × years, EMI = (principal + interest) / months
/// - Grace period: interest-only months
/// - Last installment absorbs rounding difference
/// - Per DO-NOT: "Calculate loan EMI with flat rate when diminishing balance is configured"
/// 
/// Source: erpnext/loan_management/doctype/loan/loan.py
/// </summary>
public class Loan : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }

    /// <summary>Loan reference number.</summary>
    public string LoanNumber { get; set; } = null!;

    public LoanType LoanType { get; set; }
    public InterestCalculationMethod InterestMethod { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Draft;

    /// <summary>Original loan principal amount.</summary>
    public decimal LoanAmount { get; set; }

    /// <summary>Annual interest rate (percentage, e.g., 5.5 = 5.5%).</summary>
    public decimal AnnualInterestRate { get; set; }

    /// <summary>Loan tenure in months.</summary>
    public int TenureMonths { get; set; }

    /// <summary>Grace period months (interest-only, no principal).</summary>
    public int GracePeriodMonths { get; set; }

    /// <summary>Date loan was disbursed.</summary>
    public DateTime? DisbursementDate { get; set; }

    /// <summary>Date first repayment is due.</summary>
    public DateTime? RepaymentStartDate { get; set; }

    /// <summary>Calculated EMI (Equated Monthly Installment).</summary>
    public decimal Emi { get; set; }

    /// <summary>Total amount repaid so far.</summary>
    public decimal TotalAmountRepaid { get; set; }

    /// <summary>Total interest charged so far.</summary>
    public decimal TotalInterestCharged { get; set; }

    /// <summary>Current outstanding balance (principal remaining).</summary>
    public decimal OutstandingBalance => LoanAmount - TotalPrincipalRepaid;

    /// <summary>Total principal repaid so far.</summary>
    public decimal TotalPrincipalRepaid { get; set; }

    /// <summary>Penalty interest rate for overdue payments (annual %).</summary>
    public decimal PenaltyRate { get; set; }

    /// <summary>GL account for the loan receivable (asset).</summary>
    public Guid? LoanAccountId { get; set; }

    /// <summary>GL account for interest income.</summary>
    public Guid? InterestIncomeAccountId { get; set; }

    /// <summary>Repayment schedule entries.</summary>
    public ICollection<LoanRepaymentSchedule> RepaymentSchedule { get; private set; }
        = new List<LoanRepaymentSchedule>();

    protected Loan() { }

    public Loan(
        Guid id,
        Guid companyId,
        Guid employeeId,
        string loanNumber,
        LoanType loanType,
        InterestCalculationMethod interestMethod,
        decimal loanAmount,
        decimal annualInterestRate,
        int tenureMonths,
        Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        EmployeeId = employeeId;
        LoanNumber = Check.NotNullOrWhiteSpace(loanNumber, nameof(loanNumber));
        LoanType = loanType;
        InterestMethod = interestMethod;
        LoanAmount = loanAmount;
        AnnualInterestRate = annualInterestRate;
        TenureMonths = tenureMonths;
        TenantId = tenantId;

        if (loanAmount <= 0)
            throw new BusinessException("MyERP:14003")
                .WithData("amount", loanAmount);

        if (tenureMonths <= 0)
            throw new BusinessException("MyERP:14004")
                .WithData("months", tenureMonths);
    }

    /// <summary>
    /// Sanction the loan (approve for disbursement).
    /// </summary>
    public void Sanction()
    {
        if (Status != LoanStatus.Draft)
            throw new BusinessException("MyERP:01001");
        Status = LoanStatus.Sanctioned;
    }

    /// <summary>
    /// Disburse the loan to the employee.
    /// </summary>
    public void Disburse(DateTime disbursementDate, DateTime repaymentStartDate)
    {
        if (Status != LoanStatus.Sanctioned)
            throw new BusinessException("MyERP:01001");

        DisbursementDate = disbursementDate;
        RepaymentStartDate = repaymentStartDate;
        Status = LoanStatus.Disbursed;

        // Calculate EMI and generate schedule
        Emi = CalculateEmi();
        GenerateRepaymentSchedule();
    }

    /// <summary>
    /// Record a repayment against the loan.
    /// </summary>
    public void RecordRepayment(decimal principalAmount, decimal interestAmount)
    {
        if (Status != LoanStatus.Disbursed && Status != LoanStatus.PartiallyRepaid)
            throw new BusinessException("MyERP:01001");

        TotalPrincipalRepaid += principalAmount;
        TotalInterestCharged += interestAmount;
        TotalAmountRepaid += principalAmount + interestAmount;

        if (TotalPrincipalRepaid >= LoanAmount)
            Status = LoanStatus.FullyRepaid;
        else
            Status = LoanStatus.PartiallyRepaid;
    }

    /// <summary>
    /// Calculate EMI based on the interest method.
    /// </summary>
    public decimal CalculateEmi()
    {
        if (InterestMethod == InterestCalculationMethod.DiminishingBalance)
            return CalculateDiminishingEmi();
        else
            return CalculateFlatEmi();
    }

    /// <summary>
    /// Calculate penalty for overdue payment.
    /// penalty = overdue_principal × (penalty_rate / 100) × overdue_days / 365
    /// </summary>
    public decimal CalculatePenalty(decimal overduePrincipal, int overdueDays)
    {
        if (PenaltyRate <= 0 || overdueDays <= 0) return 0;
        return Math.Round(overduePrincipal * (PenaltyRate / 100m) * overdueDays / 365m, 2);
    }

    private decimal CalculateDiminishingEmi()
    {
        // EMI = P × r × (1+r)^n / ((1+r)^n - 1)
        var monthlyRate = AnnualInterestRate / 100m / 12m;
        if (monthlyRate == 0) return LoanAmount / TenureMonths;

        var n = TenureMonths - GracePeriodMonths; // Principal repayment months
        if (n <= 0) return 0;

        var factor = (decimal)Math.Pow((double)(1 + monthlyRate), n);
        var emi = LoanAmount * monthlyRate * factor / (factor - 1);
        return Math.Round(emi, 2);
    }

    private decimal CalculateFlatEmi()
    {
        // total_interest = principal × rate × tenure_years
        var totalInterest = LoanAmount * (AnnualInterestRate / 100m) * (TenureMonths / 12m);
        var totalRepayable = LoanAmount + totalInterest;
        return Math.Round(totalRepayable / TenureMonths, 2);
    }

    private void GenerateRepaymentSchedule()
    {
        if (LoanType == LoanType.DemandLoan || !RepaymentStartDate.HasValue) return;

        RepaymentSchedule.Clear();
        var outstanding = LoanAmount;
        var monthlyRate = AnnualInterestRate / 100m / 12m;

        for (int month = 1; month <= TenureMonths; month++)
        {
            var paymentDate = RepaymentStartDate.Value.AddMonths(month - 1);
            decimal interest, principal;

            if (month <= GracePeriodMonths)
            {
                // Grace period: interest-only
                interest = Math.Round(outstanding * monthlyRate, 2);
                principal = 0;
            }
            else if (InterestMethod == InterestCalculationMethod.DiminishingBalance)
            {
                interest = Math.Round(outstanding * monthlyRate, 2);
                principal = Emi - interest;
            }
            else // Flat rate
            {
                interest = Math.Round(LoanAmount * (AnnualInterestRate / 100m) / 12m, 2);
                principal = Math.Round(LoanAmount / (TenureMonths - GracePeriodMonths), 2);
            }

            // Last installment absorbs rounding
            if (month == TenureMonths)
                principal = outstanding;

            outstanding -= principal;

            RepaymentSchedule.Add(new LoanRepaymentSchedule(
                Guid.NewGuid(), Id, month, paymentDate, principal, interest,
                principal + interest, Math.Max(0, outstanding)));
        }
    }
}

/// <summary>
/// A single installment in the loan repayment schedule.
/// </summary>
public class LoanRepaymentSchedule : Entity<Guid>
{
    public Guid LoanId { get; set; }
    public int InstallmentNumber { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal TotalPayment { get; set; }
    public decimal OutstandingAfterPayment { get; set; }
    public bool IsPaid { get; set; }

    protected LoanRepaymentSchedule() { }

    public LoanRepaymentSchedule(Guid id, Guid loanId, int installmentNumber,
        DateTime paymentDate, decimal principal, decimal interest,
        decimal totalPayment, decimal outstandingAfter) : base(id)
    {
        LoanId = loanId;
        InstallmentNumber = installmentNumber;
        PaymentDate = paymentDate;
        PrincipalAmount = principal;
        InterestAmount = interest;
        TotalPayment = totalPayment;
        OutstandingAfterPayment = outstandingAfter;
    }
}
