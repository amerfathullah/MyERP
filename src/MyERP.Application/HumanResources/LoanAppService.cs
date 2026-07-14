using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

public class LoanDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public string LoanNumber { get; set; } = null!;
    public int LoanType { get; set; }
    public int InterestMethod { get; set; }
    public int Status { get; set; }
    public decimal LoanAmount { get; set; }
    public decimal AnnualInterestRate { get; set; }
    public int TenureMonths { get; set; }
    public int GracePeriodMonths { get; set; }
    public decimal Emi { get; set; }
    public decimal TotalAmountRepaid { get; set; }
    public decimal OutstandingBalance { get; set; }
    public DateTime? DisbursementDate { get; set; }
    public DateTime? RepaymentStartDate { get; set; }
    public LoanRepaymentScheduleDto[] Schedule { get; set; } = [];
}

public class LoanRepaymentScheduleDto
{
    public DateTime PaymentDate { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal TotalPayment { get; set; }
    public decimal OutstandingBalance { get; set; }
    public bool IsPaid { get; set; }
}

public class CreateLoanDto
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public int LoanType { get; set; }
    public int InterestMethod { get; set; }
    public decimal LoanAmount { get; set; }
    public decimal AnnualInterestRate { get; set; }
    public int TenureMonths { get; set; }
    public int GracePeriodMonths { get; set; }
}

public class DisburseLoanDto
{
    public DateTime DisbursementDate { get; set; }
    public DateTime RepaymentStartDate { get; set; }
}

public class RecordRepaymentDto
{
    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
}

/// <summary>
/// AppService for Employee Loan management.
/// Supports EMI calculation, repayment schedule generation, disbursement and repayment tracking.
/// Per ERPNext: Diminishing Balance EMI = P×r×(1+r)^n / ((1+r)^n - 1)
/// Per DO-NOT: "Calculate loan EMI with flat rate when diminishing balance is configured"
/// </summary>
[Authorize(MyERPPermissions.Employees.Default)]
public class LoanAppService : ApplicationService
{
    private readonly IRepository<Loan, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public LoanAppService(
        IRepository<Loan, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
    }

    public async Task<PagedResultDto<LoanDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderByDescending(l => l.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<LoanDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public async Task<LoanDto> GetAsync(Guid id)
    {
        var loan = (await _repository.WithDetailsAsync()).First(l => l.Id == id);
        return MapToDto(loan);
    }

    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<LoanDto> CreateAsync(CreateLoanDto input)
    {
        var number = await _numberGenerator.GenerateAsync("LOAN", input.CompanyId);

        var loan = new Loan(
            GuidGenerator.Create(), input.CompanyId, input.EmployeeId, number,
            (LoanType)input.LoanType, (InterestCalculationMethod)input.InterestMethod,
            input.LoanAmount, input.AnnualInterestRate, input.TenureMonths,
            CurrentTenant.Id)
        {
            GracePeriodMonths = input.GracePeriodMonths,
        };

        await _repository.InsertAsync(loan);
        return MapToDto(loan);
    }

    /// <summary>Approve the loan for disbursement.</summary>
    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<LoanDto> SanctionAsync(Guid id)
    {
        var loan = (await _repository.WithDetailsAsync()).First(l => l.Id == id);
        loan.Sanction();
        await _repository.UpdateAsync(loan);
        return MapToDto(loan);
    }

    /// <summary>Disburse the loan and generate the repayment schedule.</summary>
    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<LoanDto> DisburseAsync(Guid id, DisburseLoanDto input)
    {
        var loan = (await _repository.WithDetailsAsync()).First(l => l.Id == id);
        loan.Disburse(input.DisbursementDate, input.RepaymentStartDate);
        await _repository.UpdateAsync(loan);
        return MapToDto(loan);
    }

    /// <summary>Record a repayment against the loan.</summary>
    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<LoanDto> RecordRepaymentAsync(Guid id, RecordRepaymentDto input)
    {
        var loan = (await _repository.WithDetailsAsync()).First(l => l.Id == id);
        loan.RecordRepayment(input.PrincipalAmount, input.InterestAmount);
        await _repository.UpdateAsync(loan);
        return MapToDto(loan);
    }

    /// <summary>Cancel the loan.</summary>
    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<LoanDto> CancelAsync(Guid id)
    {
        var loan = (await _repository.WithDetailsAsync()).First(l => l.Id == id);
        if (loan.Status is LoanStatus.FullyRepaid or LoanStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        loan.Status = LoanStatus.Cancelled;
        await _repository.UpdateAsync(loan);
        return MapToDto(loan);
    }

    private static LoanDto MapToDto(Loan l) => new()
    {
        Id = l.Id,
        CompanyId = l.CompanyId,
        EmployeeId = l.EmployeeId,
        LoanNumber = l.LoanNumber,
        LoanType = (int)l.LoanType,
        InterestMethod = (int)l.InterestMethod,
        Status = (int)l.Status,
        LoanAmount = l.LoanAmount,
        AnnualInterestRate = l.AnnualInterestRate,
        TenureMonths = l.TenureMonths,
        GracePeriodMonths = l.GracePeriodMonths,
        Emi = l.Emi,
        TotalAmountRepaid = l.TotalAmountRepaid,
        OutstandingBalance = l.OutstandingBalance,
        DisbursementDate = l.DisbursementDate,
        RepaymentStartDate = l.RepaymentStartDate,
        Schedule = l.RepaymentSchedule.OrderBy(s => s.PaymentDate).Select(s => new LoanRepaymentScheduleDto
        {
            PaymentDate = s.PaymentDate,
            PrincipalAmount = s.PrincipalAmount,
            InterestAmount = s.InterestAmount,
            TotalPayment = s.TotalPayment,
            OutstandingBalance = s.OutstandingAfterPayment,
            IsPaid = s.IsPaid,
        }).ToArray(),
    };
}
