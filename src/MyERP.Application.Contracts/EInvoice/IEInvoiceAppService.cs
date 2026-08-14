using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.EInvoice;

public interface IEInvoiceAppService : IApplicationService
{
    Task<EInvoiceSubmissionDto> SubmitAsync(SubmitEInvoiceDto input);
    Task<BatchSubmitResultDto> BatchSubmitAsync(BatchSubmitEInvoiceDto input);
    Task<EInvoiceSubmissionDto> GetStatusAsync(Guid submissionId);
    Task<EInvoiceSubmissionDto> CancelAsync(CancelEInvoiceDto input);
    Task<PagedResultDto<EInvoiceSubmissionDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<EInvoiceSubmissionDto> SubmitConsolidatedAsync(SubmitEInvoiceDto input);
    Task<List<Guid>> ConsolidateInvoicesAsync(ConsolidateInvoicesDto input);
    Task<TaxpayerSearchResultDto> SearchTaxpayerAsync(SearchTaxpayerDto input);
}
