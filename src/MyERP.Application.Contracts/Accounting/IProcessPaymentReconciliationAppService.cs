using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IProcessPaymentReconciliationAppService : IApplicationService
{
    Task<PagedResultDto<ProcessPaymentReconciliationDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<ProcessPaymentReconciliationDto> GetAsync(Guid id);
    Task<ProcessPaymentReconciliationDto> CreateAsync(CreateProcessPaymentReconciliationDto input);

    /// <summary>Draft -&gt; Queued and enqueues the background job.</summary>
    Task<ProcessPaymentReconciliationDto> SubmitAsync(Guid id);

    Task<ProcessPaymentReconciliationDto> CancelAsync(Guid id);
}
