using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IPurchaseInvoiceAppService : IApplicationService
{
    Task<PurchaseInvoiceDto> GetAsync(Guid id);
    Task<PagedResultDto<PurchaseInvoiceDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<PurchaseInvoiceDto> CreateAsync(CreatePurchaseInvoiceDto input);
    Task<PurchaseInvoiceDto> SubmitAsync(Guid id);
    Task<PurchaseInvoiceDto> PostAsync(Guid id);
    Task<PurchaseInvoiceDto> CancelAsync(Guid id);
}
