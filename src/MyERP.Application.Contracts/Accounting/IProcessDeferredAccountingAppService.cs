using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IProcessDeferredAccountingAppService : IApplicationService
{
    Task<PagedResultDto<ProcessDeferredAccountingDto>> GetListAsync(ProcessDeferredAccountingGetListInput input);
    Task<ProcessDeferredAccountingDto> GetAsync(Guid id);
    Task<ProcessDeferredAccountingDto> CreateAsync(CreateProcessDeferredAccountingDto input);
    Task<ProcessDeferredAccountingDto> UpdateAsync(Guid id, UpdateProcessDeferredAccountingDto input);
    Task DeleteAsync(Guid id);
    Task<ProcessDeferredAccountingDto> SubmitAsync(Guid id);
    Task<ProcessDeferredAccountingDto> CancelAsync(Guid id);
}
