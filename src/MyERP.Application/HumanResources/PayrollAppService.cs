using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Core.DomainServices;
using MyERP.HumanResources.DomainServices;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

[Authorize(MyERPPermissions.Payroll.Default)]
public class PayrollAppService : ApplicationService, IPayrollAppService
{
    private readonly IRepository<PayrollEntry, Guid> _repository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<SalarySlip, Guid> _salarySlipRepository;
    private readonly IRepository<SalaryComponent, Guid> _salaryComponentRepository;
    private readonly PayrollEngine _payrollEngine;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public PayrollAppService(
        IRepository<PayrollEntry, Guid> repository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<SalarySlip, Guid> salarySlipRepository,
        IRepository<SalaryComponent, Guid> salaryComponentRepository,
        PayrollEngine payrollEngine,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
        _salarySlipRepository = salarySlipRepository;
        _salaryComponentRepository = salaryComponentRepository;
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
        if (input.Month < 1 || input.Month > 12)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        var existingQ = await _repository.GetQueryableAsync();
        var alreadyExists = existingQ.Any(p => p.CompanyId == input.CompanyId && p.Year == input.Year && p.Month == input.Month && p.Status != Core.DocumentStatus.Cancelled);
        if (alreadyExists)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DuplicatePayrollEntry)
                .WithData("year", input.Year)
                .WithData("month", input.Month);
        }

        var payrollNumber = await _numberGenerator.GenerateAsync("Payroll", input.CompanyId);
        var postingDate = new DateTime(input.Year, input.Month, DateTime.DaysInMonth(input.Year, input.Month));

        var entry = new PayrollEntry(
            GuidGenerator.Create(), input.CompanyId, payrollNumber,
            input.Year, input.Month, postingDate);

        // Get all active employees for the company
        var employees = await _employeeRepository.GetListAsync(e =>
            e.CompanyId == input.CompanyId && e.Status == EmploymentStatus.Active);

        // Resolve standard Salary Components by name for the individual Salary Slips (best-effort;
        // SalarySlipComponent has no FK on SalaryComponentId, so a miss just falls back to Guid.Empty).
        var components = await _salaryComponentRepository.GetListAsync();
        Guid ComponentId(string name) => components
            .FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty;
        var basicSalaryComponentId = ComponentId("Basic Salary");
        var epfComponentId = ComponentId("EPF");
        var socsoComponentId = ComponentId("SOCSO");
        var eisComponentId = ComponentId("EIS");
        var pcbComponentId = ComponentId("PCB");
        var loanComponentId = ComponentId("Loan Repayment");

        var salarySlips = new List<SalarySlip>();
        var totalWorkingDays = DateTime.DaysInMonth(input.Year, input.Month);

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

            // Build the individual Salary Slip for this employee (payslip view, per ERPNext salary_slip).
            // Only the employee-side deductions are shown; employer contributions don't affect net pay.
            var payLine = entry.Lines.Last();
            var slip = new SalarySlip(GuidGenerator.Create(), input.CompanyId, employee.Id,
                periodStart, periodEnd, postingDate, CurrentTenant.Id)
            {
                EmployeeName = employee.FullName,
                PayrollEntryId = entry.Id,
                TotalWorkingDays = totalWorkingDays,
                PaymentDays = totalWorkingDays - (int)unpaidDays,
                LeavesWithoutPay = (int)unpaidDays,
            };
            slip.AddEarning(basicSalaryComponentId, "Basic Salary", payLine.GrossSalary);
            if (payLine.EpfEmployee > 0) slip.AddDeduction(epfComponentId, "EPF", payLine.EpfEmployee, isStatutory: true);
            if (payLine.SocsoEmployee > 0) slip.AddDeduction(socsoComponentId, "SOCSO", payLine.SocsoEmployee, isStatutory: true);
            if (payLine.EisEmployee > 0) slip.AddDeduction(eisComponentId, "EIS", payLine.EisEmployee, isStatutory: true);
            if (payLine.Pcb > 0) slip.AddDeduction(pcbComponentId, "PCB", payLine.Pcb, isStatutory: true);
            if (payLine.LoanDeduction > 0) slip.AddDeduction(loanComponentId, "Loan Repayment", payLine.LoanDeduction);
            salarySlips.Add(slip);
        }

        await _repository.InsertAsync(entry, autoSave: true);
        foreach (var slip in salarySlips)
            await _salarySlipRepository.InsertAsync(slip, autoSave: true);

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

        // Submit the linked Salary Slips (individual payslips) alongside the payroll run
        var slipsQuery = await _salarySlipRepository.GetQueryableAsync();
        foreach (var slip in slipsQuery.Where(s => s.PayrollEntryId == entry.Id && s.Status == Core.DocumentStatus.Draft).ToList())
        {
            slip.Submit();
            await _salarySlipRepository.UpdateAsync(slip);
        }

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

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PayrollEntry", entry.Id,
            "Submitted", entry.CompanyId,
            entry.PayrollNumber, "Draft", "Submitted", CurrentUser.Id,
            $"Payroll Entry {entry.PayrollNumber} ({entry.Month}/{entry.Year}) submitted", CurrentTenant.Id));

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

        // Cancel the linked Salary Slips alongside the payroll run
        var slipsQuery = await _salarySlipRepository.GetQueryableAsync();
        foreach (var slip in slipsQuery.Where(s => s.PayrollEntryId == entry.Id && s.Status == Core.DocumentStatus.Submitted).ToList())
        {
            slip.Cancel();
            await _salarySlipRepository.UpdateAsync(slip);
        }

        entry.Cancel();
        await _repository.UpdateAsync(entry, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PayrollEntry", entry.Id,
            "Cancelled", entry.CompanyId,
            entry.PayrollNumber, "Submitted", "Cancelled", CurrentUser.Id,
            $"Payroll Entry {entry.PayrollNumber} ({entry.Month}/{entry.Year}) cancelled", CurrentTenant.Id));

        return ObjectMapper.Map<PayrollEntry, PayrollEntryDto>(entry);
    }

    [Authorize(MyERPPermissions.Payroll.Default)]
    public async Task<PayrollPreviewDto> GetEmployeePreviewAsync(CreatePayrollEntryDto input)
    {
        var employees = await _employeeRepository.GetListAsync(e =>
            e.CompanyId == input.CompanyId && e.Status == EmploymentStatus.Active);

        var eligible = employees.Where(e => e.BasicSalary.HasValue && e.BasicSalary.Value > 0).ToList();

        return new PayrollPreviewDto
        {
            EmployeeCount = eligible.Count,
            EstimatedGrossTotal = eligible.Sum(e => e.BasicSalary!.Value),
            Employees = eligible.Select(e => new PayrollEmployeePreviewDto
            {
                EmployeeId = e.Id,
                EmployeeName = e.FullName,
                Department = e.Department,
                Designation = e.Designation,
                BasicSalary = e.BasicSalary!.Value,
            }).ToList(),
        };
    }

    /// <summary>
    /// Create Bank Entry (Journal Entry) for a submitted payroll.
    /// Per ERPNext payroll_entry.py make_bank_entry():
    /// DR Salary Payable → CR Bank for total net salary.
    /// </summary>
    [Authorize(MyERPPermissions.Payroll.Submit)]
    public async Task<PayrollBankEntryResultDto> CreateBankEntryAsync(CreatePayrollBankEntryDto input)
    {
        var entry = await _repository.GetAsync(input.PayrollEntryId);

        if (entry.Status != Core.DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("expected", "Submitted")
                .WithData("actual", entry.Status.ToString());

        if (entry.TotalNetSalary <= 0)
            throw new BusinessException("MyERP:Payroll:NoNetSalary");

        var fiscalYearRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Accounting.Entities.FiscalYear, Guid>>();
        var numberGen = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.IDocumentNumberGenerator>();

        var paymentDate = input.PaymentDate ?? entry.PostingDate;

        // Resolve fiscal year
        var fyQuery = await fiscalYearRepo.GetQueryableAsync();
        var fy = fyQuery.FirstOrDefault(f =>
            f.CompanyId == entry.CompanyId && f.StartDate <= paymentDate && f.EndDate >= paymentDate);
        if (fy == null)
            throw new BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("postingDate", paymentDate.ToString("yyyy-MM-dd"));

        var jeNumber = await numberGen.GenerateAsync("JE", entry.CompanyId);
        var je = new Accounting.Entities.JournalEntry(
            GuidGenerator.Create(), entry.CompanyId, fy.Id, paymentDate, CurrentTenant.Id)
        {
            EntryNumber = jeNumber,
            VoucherType = Accounting.JournalEntryVoucherType.BankEntry,
            ReferenceType = "PayrollEntry",
            ReferenceId = entry.Id,
            ReferenceNumber = entry.PayrollNumber,
            Narration = $"Salary payment for {entry.PeriodLabel}",
        };

        // Find Salary Payable account (AccountSubType = AccountsPayable with "Salary" in name)
        var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Accounting.Entities.Account, Guid>>();
        var accounts = await accountRepo.GetQueryableAsync();

        var salaryPayableAccount = accounts
            .FirstOrDefault(a => a.CompanyId == entry.CompanyId
                && a.AccountSubType == Accounting.AccountSubType.AccountsPayable
                && a.AccountName != null && a.AccountName.Contains("Salary"));

        // Fallback: any AccountsPayable account
        salaryPayableAccount ??= accounts
            .FirstOrDefault(a => a.CompanyId == entry.CompanyId
                && a.AccountSubType == Accounting.AccountSubType.AccountsPayable);

        if (salaryPayableAccount == null)
            throw new BusinessException("MyERP:Payroll:NoPayableAccount");

        // DR Salary Payable (reduces the payable liability)
        je.AddLine(salaryPayableAccount.Id, entry.TotalNetSalary, true, "Salary Payable");

        // CR Bank (payment out)
        je.AddLine(input.BankAccountId, entry.TotalNetSalary, false, "Bank Payment - Salaries");

        var jeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Accounting.Entities.JournalEntry, Guid>>();
        await jeRepo.InsertAsync(je, autoSave: true);

        return new PayrollBankEntryResultDto
        {
            JournalEntryId = je.Id,
            JournalEntryNumber = je.EntryNumber!,
            TotalAmount = entry.TotalNetSalary,
            EmployeeCount = entry.Lines.Count,
        };
    }
}
