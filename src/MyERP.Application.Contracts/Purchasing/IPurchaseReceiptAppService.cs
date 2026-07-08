using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IPurchaseReceiptAppService : IApplicationService
{
    Task<PurchaseReceiptDto> GetAsync(Guid id);
    Task<PagedResultDto<PurchaseReceiptDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<PurchaseReceiptDto> CreateAsync(CreatePurchaseReceiptDto input);
    Task<PurchaseReceiptDto> SubmitAsync(Guid id);
    Task<PurchaseReceiptDto> CancelAsync(Guid id);
}
