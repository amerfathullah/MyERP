using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public class GetPayrollListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public string? Filter { get; set; }
    public string? Status { get; set; }
}

public interface IPayrollAppService : IApplicationService
{
    Task<PayrollEntryDto> GetAsync(Guid id);
    Task<PagedResultDto<PayrollEntryDto>> GetListAsync(GetPayrollListDto input);
    /// <summary>Create a payroll entry and auto-calculate all active employees' salaries.</summary>
    Task<PayrollEntryDto> CreateAsync(CreatePayrollEntryDto input);
    Task<PayrollEntryDto> SubmitAsync(Guid id);
    Task<PayrollEntryDto> CancelAsync(Guid id);

    /// <summary>
    /// Create a bank payment Journal Entry for a submitted payroll.
    /// Per ERPNext: "Make Bank Entry" — debits Salary Payable, credits Bank account
    /// for the total net salary amount.
    /// </summary>
    Task<PayrollBankEntryResultDto> CreateBankEntryAsync(CreatePayrollBankEntryDto input);

    /// <summary>
    /// Get a summary of employees and amounts before creating payroll.
    /// Per ERPNext: "Get Employees" step in Payroll Entry wizard.
    /// </summary>
    Task<PayrollPreviewDto> GetEmployeePreviewAsync(CreatePayrollEntryDto input);
}
