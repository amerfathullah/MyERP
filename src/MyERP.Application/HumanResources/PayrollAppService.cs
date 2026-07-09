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
        return MapToDto(entry);
    }

    public async Task<PagedResultDto<PayrollEntryDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var queryable = await _repository.WithDetailsAsync();
        var totalCount = await AsyncExecuter.CountAsync(queryable);

        var entries = await AsyncExecuter.ToListAsync(
            queryable.OrderByDescending(e => e.Year).ThenByDescending(e => e.Month)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        return new PagedResultDto<PayrollEntryDto>(totalCount, entries.Select(MapToDto).ToList());
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

            var context = new PayrollContext
            {
                EmployeeId = employee.Id,
                GrossSalary = employee.BasicSalary.Value,
                PayrollDate = postingDate,
                EmployeeAge = age,
                Citizenship = employee.Citizenship,
            };

            var calc = await _payrollEngine.CalculateAsync(context);

            entry.AddLine(employee.Id, employee.FullName, calc.GrossSalary,
                calc.EpfEmployee, calc.EpfEmployer, calc.SocsoEmployee, calc.SocsoEmployer,
                calc.EisEmployee, calc.EisEmployer, calc.Pcb);
        }

        await _repository.InsertAsync(entry, autoSave: true);
        return MapToDto(entry);
    }

    [Authorize(MyERPPermissions.Payroll.Submit)]
    public async Task<PayrollEntryDto> SubmitAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Submit();
        await _repository.UpdateAsync(entry, autoSave: true);
        return MapToDto(entry);
    }

    [Authorize(MyERPPermissions.Payroll.Cancel)]
    public async Task<PayrollEntryDto> CancelAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Cancel();
        await _repository.UpdateAsync(entry, autoSave: true);
        return MapToDto(entry);
    }

    private static PayrollEntryDto MapToDto(PayrollEntry e) => new()
    {
        Id = e.Id,
        CompanyId = e.CompanyId,
        PayrollNumber = e.PayrollNumber,
        Year = e.Year,
        Month = e.Month,
        PeriodLabel = e.PeriodLabel,
        PostingDate = e.PostingDate,
        TotalGrossSalary = e.TotalGrossSalary,
        TotalDeductions = e.TotalDeductions,
        TotalNetSalary = e.TotalNetSalary,
        TotalEmployerContributions = e.TotalEmployerContributions,
        Status = e.Status.ToString(),
        Lines = e.Lines.Select(l => new PayrollEntryLineDto
        {
            Id = l.Id,
            EmployeeId = l.EmployeeId,
            EmployeeName = l.EmployeeName,
            GrossSalary = l.GrossSalary,
            EpfEmployee = l.EpfEmployee,
            EpfEmployer = l.EpfEmployer,
            SocsoEmployee = l.SocsoEmployee,
            SocsoEmployer = l.SocsoEmployer,
            EisEmployee = l.EisEmployee,
            EisEmployer = l.EisEmployer,
            Pcb = l.Pcb,
            TotalDeductions = l.TotalDeductions,
            NetSalary = l.NetSalary,
        }).ToList(),
    };
}
