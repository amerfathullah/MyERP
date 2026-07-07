using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesInvoiceAppService : IApplicationService
{
    Task<SalesInvoiceDto> GetAsync(Guid id);
    Task<PagedResultDto<SalesInvoiceDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<SalesInvoiceDto> CreateAsync(CreateSalesInvoiceDto input);
    Task<SalesInvoiceDto> SubmitAsync(Guid id);
    Task<SalesInvoiceDto> PostAsync(Guid id);
    Task<SalesInvoiceDto> CancelAsync(Guid id);
}
