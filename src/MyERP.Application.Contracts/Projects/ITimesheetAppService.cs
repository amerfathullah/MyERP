using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Projects;

public interface ITimesheetAppService : IApplicationService
{
    Task<TimesheetDto> GetAsync(Guid id);
    Task<PagedResultDto<TimesheetDto>> GetListAsync(GetTimesheetListDto input);
    Task<TimesheetDto> CreateAsync(CreateTimesheetDto input);
    Task<TimesheetDto> UpdateAsync(Guid id, CreateTimesheetDto input);
    Task<TimesheetDto> SubmitAsync(Guid id);
    Task<TimesheetDto> CancelAsync(Guid id);
    Task<TimesheetBillingResultDto> CreateInvoiceFromTimesheetsAsync(CreateTimesheetInvoiceDto input);
    Task<List<UnbilledTimesheetSummaryDto>> GetUnbilledSummaryAsync(Guid companyId, Guid? projectId, DateTime? fromDate = null, DateTime? toDate = null);
}
