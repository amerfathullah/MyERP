using System;
using System.Collections.Generic;
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
    Task<PurchaseReceiptDto> CloseAsync(Guid id);
    Task<PurchaseReceiptDto> ReopenAsync(Guid id);
    Task<PurchaseReceiptDto> CloseItemAsync(Guid id, Guid itemId);
    Task<PurchaseReceiptDto> ReopenItemAsync(Guid id, Guid itemId);
    Task<BulkOperationResultDto> BulkSubmitAsync(List<Guid> ids);
}
