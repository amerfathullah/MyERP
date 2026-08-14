using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IProformaInvoiceAppService : IApplicationService
{
    Task<ProformaInvoiceDto> GetAsync(Guid id);
    Task<PagedResultDto<ProformaInvoiceDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<List<ProformaInvoiceDto>> GetForSalesOrderAsync(Guid salesOrderId);
    Task<List<ProformedTotalsDto>> GetProformedTotalsAsync(Guid salesOrderId);
    Task<ProformaInvoiceDto> CreateAsync(CreateProformaInvoiceDto input);
    Task CancelAsync(Guid id);
    Task SendEmailAsync(Guid id, SendProformaEmailDto input);
}
