using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.HumanResources.DomainServices;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

[Authorize(MyERPPermissions.Employees.Default)]
public class LoanAppService : ApplicationService, ILoanAppService
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

    public async Task<PagedResultDto<LoanDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter; query = query.Where(x => x.LoanNumber.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<LoanStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(l => l.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<LoanDto>(totalCount, items.Select(ObjectMapper.Map<Loan, LoanDto>).ToList());
    }

    public async Task<LoanDto> GetAsync(Guid id)
    {
        var loan = (await _repository.WithDetailsAsync()).First(l => l.Id == id);
        return ObjectMapper.Map<Loan, LoanDto>(loan);
    }

    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<LoanDto> CreateAsync(CreateLoanDto input)
    {
        if (input.LoanAmount <= 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "LoanAmount");
        }

        if (input.TenureMonths <= 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "TenureMonths");
        }

        if (input.AnnualInterestRate < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "AnnualInterestRate");
        }

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
        return ObjectMapper.Map<Loan, LoanDto>(loan);
    }

    /// <summary>Approve the loan for disbursement.</summary>
    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<LoanDto> SanctionAsync(Guid id)
    {
        var loan = (await _repository.WithDetailsAsync()).First(l => l.Id == id);
        loan.Sanction();
        await _repository.UpdateAsync(loan);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Loan", loan.Id,
            "Sanctioned", loan.CompanyId,
            loan.LoanNumber, "Draft", "Sanctioned", CurrentUser.Id,
            $"Loan {loan.LoanNumber} ({loan.LoanAmount:C}) sanctioned", CurrentTenant.Id));

        return ObjectMapper.Map<Loan, LoanDto>(loan);
    }

    /// <summary>Disburse the loan and generate the repayment schedule.</summary>
    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<LoanDto> DisburseAsync(Guid id, DisburseLoanDto input)
    {
        var loan = (await _repository.WithDetailsAsync()).First(l => l.Id == id);
        loan.Disburse(input.DisbursementDate, input.RepaymentStartDate);
        await _repository.UpdateAsync(loan);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Loan", loan.Id,
            "Disbursed", loan.CompanyId,
            loan.LoanNumber, "Sanctioned", "Disbursed", CurrentUser.Id,
            $"Loan {loan.LoanNumber} disbursed on {input.DisbursementDate:yyyy-MM-dd}", CurrentTenant.Id));

        return ObjectMapper.Map<Loan, LoanDto>(loan);
    }

    /// <summary>Record a repayment against the loan.
    /// When only total amount is provided (PrincipalAmount=0 AND InterestAmount=0),
    /// uses LoanManager to auto-split into principal+interest per the loan's interest method.
    /// Per DO-NOT: "Calculate loan EMI with flat rate when diminishing balance is configured"
    /// </summary>
    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<LoanDto> RecordRepaymentAsync(Guid id, RecordRepaymentDto input)
    {
        var loan = (await _repository.WithDetailsAsync()).First(l => l.Id == id);

        decimal principal = input.PrincipalAmount;
        decimal interest = input.InterestAmount;

        // Auto-split when caller provides only total (e.g., payroll deduction path)
        if (principal == 0 && interest == 0 && input.TotalAmount > 0)
        {
            var loanManager = LazyServiceProvider.LazyGetRequiredService<LoanManager>();
            (principal, interest) = loanManager.SplitRepayment(loan, input.TotalAmount);
        }

        loan.RecordRepayment(principal, interest);
        await _repository.UpdateAsync(loan);
        return ObjectMapper.Map<Loan, LoanDto>(loan);
    }

    /// <summary>Cancel the loan.</summary>
    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<LoanDto> CancelAsync(Guid id)
    {
        var loan = (await _repository.WithDetailsAsync()).First(l => l.Id == id);
        loan.Cancel();
        await _repository.UpdateAsync(loan);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Loan", loan.Id,
            "Cancelled", loan.CompanyId,
            loan.LoanNumber, loan.Status.ToString(), "Cancelled", CurrentUser.Id,
            $"Loan {loan.LoanNumber} cancelled", CurrentTenant.Id));

        return ObjectMapper.Map<Loan, LoanDto>(loan);
    }
}

