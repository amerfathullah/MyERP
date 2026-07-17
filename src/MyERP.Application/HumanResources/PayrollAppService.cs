using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.HumanResources.DomainServices;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

[Authorize(MyERPPermissions.Payroll.Default)]
public class PayrollAppService : ApplicationService, IPayrollAppService
{
    private readonly IRepository<PayrollEntry, Guid> _repository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly PayrollEngine _payrollEngine;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public PayrollAppService(
        IRepository<PayrollEntry, Guid> repository,
        IRepository<Employee, Guid> employeeRepository,
        PayrollEngine payrollEngine,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
        _payrollEngine = payrollEngine;
        _numberGenerator = numberGenerator;
    }

    public async Task<PayrollEntryDto> GetAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        return ObjectMapper.Map<PayrollEntry, PayrollEntryDto>(entry);
    }

    public async Task<PagedResultDto<PayrollEntryDto>> GetListAsync(GetPayrollListDto input)
    {
        var queryable = await _repository.WithDetailsAsync();
        if (input.CompanyId.HasValue)
            queryable = queryable.Where(e => e.CompanyId == input.CompanyId.Value);
        var totalCount = await AsyncExecuter.CountAsync(queryable);

        var entries = await AsyncExecuter.ToListAsync(
            queryable.OrderByDescending(e => e.Year).ThenByDescending(e => e.Month)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        return new PagedResultDto<PayrollEntryDto>(totalCount, entries.Select(x => ObjectMapper.Map<PayrollEntry, PayrollEntryDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.Payroll.Create)]
    public async Task<PayrollEntryDto> CreateAsync(CreatePayrollEntryDto input)
    {
        var payrollNumber = await _numberGenerator.GenerateAsync("Payroll", input.CompanyId);
        var postingDate = new DateTime(input.Year, input.Month, DateTime.DaysInMonth(input.Year, input.Month));

        var entry = new PayrollEntry(
            GuidGenerator.Create(), input.CompanyId, payrollNumber,
            input.Year, input.Month, postingDate);

        // Get all active employees for the company
        var employees = await _employeeRepository.GetListAsync(e =>
            e.CompanyId == input.CompanyId && e.Status == EmploymentStatus.Active);

        foreach (var employee in employees)
        {
            if (!employee.BasicSalary.HasValue || employee.BasicSalary.Value <= 0)
                continue;

            var age = employee.DateOfBirth.HasValue
                ? (int)((postingDate - employee.DateOfBirth.Value).TotalDays / 365.25)
                : 30;

            // Check for unpaid leave (LWP) and prorate salary
            var lwpService = LazyServiceProvider.LazyGetRequiredService<HumanResources.DomainServices.UnpaidLeaveProrationService>();
            var periodStart = new DateTime(input.Year, input.Month, 1);
            var periodEnd = new DateTime(input.Year, input.Month, DateTime.DaysInMonth(input.Year, input.Month));
            var unpaidDays = await lwpService.GetUnpaidLeaveDaysAsync(employee.Id, periodStart, periodEnd);
            var grossSalary = employee.BasicSalary.Value;

            if (unpaidDays > 0)
            {
                var (proratedGross, _, _) = UnpaidLeaveProrationService.CalculateProration(
                    grossSalary, unpaidDays);
                grossSalary = proratedGross;
            }

            var context = new PayrollContext
            {
                EmployeeId = employee.Id,
                GrossSalary = grossSalary,
                PayrollDate = postingDate,
                EmployeeAge = age,
                Citizenship = employee.Citizenship,
            };

            var calc = await _payrollEngine.CalculateAsync(context);

            entry.AddLine(employee.Id, employee.FullName, calc.GrossSalary,
                calc.EpfEmployee, calc.EpfEmployer, calc.SocsoEmployee, calc.SocsoEmployer,
                calc.EisEmployee, calc.EisEmployer, calc.Pcb);

            // Auto-deduct loan EMI if employee has an active disbursed loan
            var loanRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.HumanResources.Entities.Loan, Guid>>();
            var loanQuery = await loanRepo.GetQueryableAsync();
            var activeLoan = loanQuery.FirstOrDefault(l =>
                l.EmployeeId == employee.Id
                && l.CompanyId == input.CompanyId
                && (l.Status == MyERP.HumanResources.LoanStatus.Disbursed
                    || l.Status == MyERP.HumanResources.LoanStatus.PartiallyRepaid)
                && l.OutstandingBalance > 0);

            if (activeLoan != null && activeLoan.Emi > 0)
            {
                // Deduct the smaller of EMI or outstanding balance
                var emiDeduction = Math.Min(activeLoan.Emi, activeLoan.OutstandingBalance);
                var lastLine = entry.Lines.Last();
                lastLine.LoanDeduction = emiDeduction;
                lastLine.LoanId = activeLoan.Id;
            }
        }

        await _repository.InsertAsync(entry, autoSave: true);
        return ObjectMapper.Map<PayrollEntry, PayrollEntryDto>(entry);
    }

    [Authorize(MyERPPermissions.Payroll.Submit)]
    public async Task<PayrollEntryDto> SubmitAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Submit();

        // Create payroll accounting entries (DR Expense, CR Payable per category)
        var postingService = LazyServiceProvider
            .LazyGetRequiredService<HumanResources.DomainServices.PayrollPostingService>();
        var journalEntryId = await postingService.PostPayrollAsync(entry);

        // Record loan repayments for employees with EMI deductions
        var loanRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.HumanResources.Entities.Loan, Guid>>();
        foreach (var line in entry.Lines.Where(l => l.LoanId.HasValue && l.LoanDeduction > 0))
        {
            var loan = await loanRepo.GetAsync(line.LoanId!.Value);
            // For simplicity, split EMI proportionally (same as schedule if available)
            var interestPortion = loan.AnnualInterestRate > 0
                ? Math.Round(loan.OutstandingBalance * loan.AnnualInterestRate / 100 / 12, 2)
                : 0;
            var principalPortion = line.LoanDeduction - interestPortion;
            if (principalPortion < 0) { interestPortion = line.LoanDeduction; principalPortion = 0; }

            loan.RecordRepayment(principalPortion, interestPortion);
            await loanRepo.UpdateAsync(loan);
        }

        await _repository.UpdateAsync(entry, autoSave: true);
        return ObjectMapper.Map<PayrollEntry, PayrollEntryDto>(entry);
    }

    [Authorize(MyERPPermissions.Payroll.Cancel)]
    public async Task<PayrollEntryDto> CancelAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);

        // Reverse loan repayments before cancelling
        var loanRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.HumanResources.Entities.Loan, Guid>>();
        foreach (var line in entry.Lines.Where(l => l.LoanId.HasValue && l.LoanDeduction > 0))
        {
            var loan = await loanRepo.FindAsync(line.LoanId!.Value);
            if (loan != null)
            {
                // Reverse the repayment by reducing TotalPrincipalRepaid
                loan.TotalPrincipalRepaid = Math.Max(0, loan.TotalPrincipalRepaid - line.LoanDeduction);
                await loanRepo.UpdateAsync(loan);
            }
        }

        // Reverse GL entries by creating a cancellation JE (opposite of posting)
        var postingOrchestrator = LazyServiceProvider
            .LazyGetRequiredService<Accounting.DomainServices.DocumentPostingOrchestrator>();
        await postingOrchestrator.ReversePleForDocumentAsync("PayrollEntry", entry.Id);

        entry.Cancel();
        await _repository.UpdateAsync(entry, autoSave: true);
        return ObjectMapper.Map<PayrollEntry, PayrollEntryDto>(entry);
    }
}
