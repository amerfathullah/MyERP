using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface IPayrollAppService : IApplicationService
{
    Task<PayrollEntryDto> GetAsync(Guid id);
    Task<PagedResultDto<PayrollEntryDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    /// <summary>Create a payroll entry and auto-calculate all active employees' salaries.</summary>
    Task<PayrollEntryDto> CreateAsync(CreatePayrollEntryDto input);
    Task<PayrollEntryDto> SubmitAsync(Guid id);
    Task<PayrollEntryDto> CancelAsync(Guid id);
}
