using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.EInvoice;

public interface IEInvoiceAppService : IApplicationService
{
    Task<EInvoiceSubmissionDto> SubmitAsync(SubmitEInvoiceDto input);
    Task<EInvoiceSubmissionDto> GetStatusAsync(Guid submissionId);
    Task<EInvoiceSubmissionDto> CancelAsync(CancelEInvoiceDto input);
    Task<PagedResultDto<EInvoiceSubmissionDto>> GetListAsync(PagedAndSortedResultRequestDto input);
}
