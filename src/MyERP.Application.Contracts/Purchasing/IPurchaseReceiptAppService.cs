using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IPurchaseReceiptAppService : IApplicationService
{
    Task<PurchaseReceiptDto> GetAsync(Guid id);
    Task<PagedResultDto<PurchaseReceiptDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<PurchaseReceiptDto> CreateAsync(CreatePurchaseReceiptDto input);
    Task<PurchaseReceiptDto> UpdateAsync(Guid id, CreatePurchaseReceiptDto input);
    Task<PurchaseReceiptDto> SubmitAsync(Guid id);
    Task<PurchaseReceiptDto> CancelAsync(Guid id);
}
