using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.HumanResources.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources.BackgroundJobs;

/// <summary>
/// Background job that syncs employee loan repayment schedules with payroll deductions
/// and checks for overdue loan installments.
/// Per ERPNext: loan_repayment.process_payroll_deduction (daily/monthly scheduler).
/// </summary>
public class EmployeeLoanDeductionJob : AsyncBackgroundJob<EmployeeLoanDeductionJobArgs>, ITransientDependency
{
    private readonly IRepository<Loan, Guid> _loanRepository;
    private readonly ILogger<EmployeeLoanDeductionJob> _logger;

    public EmployeeLoanDeductionJob(
        IRepository<Loan, Guid> loanRepository,
        ILogger<EmployeeLoanDeductionJob> logger)
    {
        _loanRepository = loanRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(EmployeeLoanDeductionJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("EmployeeLoanDeductionJob: Processing loan schedules for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var query = await _loanRepository.WithDetailsAsync(l => l.RepaymentSchedule);
        var activeLoans = query
            .Where(l => l.CompanyId == args.CompanyId &&
                        (l.Status == LoanStatus.Disbursed || l.Status == LoanStatus.PartiallyRepaid))
            .ToList();

        if (!activeLoans.Any())
            return;

        var updatedLoans = 0;
        foreach (var loan in activeLoans)
        {
            var dueInstallments = loan.RepaymentSchedule
                .Where(s => !s.IsPaid && s.PaymentDate <= asOfDate)
                .ToList();

            if (!dueInstallments.Any())
                continue;

            // Check if any overdue installments attract penalty
            if (loan.PenaltyRate > 0)
            {
                foreach (var installment in dueInstallments)
                {
                    var overdueDays = (int)(asOfDate - installment.PaymentDate).TotalDays;
                    if (overdueDays > 0)
                    {
                        var penalty = loan.CalculatePenalty(installment.PrincipalAmount, overdueDays);
                        if (penalty > 0)
                        {
                            _logger.LogInformation("EmployeeLoanDeductionJob: Calculated penalty MYR {Penalty} on loan {LoanNumber} installment {InstallmentNumber}",
                                penalty, loan.LoanNumber, installment.InstallmentNumber);
                        }
                    }
                }
            }

            // Check if fully repaid
            if (loan.TotalPrincipalRepaid >= loan.LoanAmount && loan.Status != LoanStatus.FullyRepaid)
            {
                loan.RecordRepayment(0, 0); // Trigger status update
                await _loanRepository.UpdateAsync(loan);
                updatedLoans++;
            }
        }

        _logger.LogInformation("EmployeeLoanDeductionJob: Processed {Total} active loans (updated {Updated}) for company {CompanyId}",
            activeLoans.Count, updatedLoans, args.CompanyId);
    }
}

public class EmployeeLoanDeductionJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
