using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Sales;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IPurchaseOrderAppService : IApplicationService
{
    Task<PurchaseOrderDto> GetAsync(Guid id);
    Task<PagedResultDto<PurchaseOrderDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderDto input);
    Task<PurchaseOrderDto> SubmitAsync(Guid id);
    Task<BulkOperationResultDto> BulkSubmitAsync(List<Guid> ids);
    Task<PurchaseOrderDto> CancelAsync(Guid id);
    Task<PurchaseOrderDto> CloseAsync(Guid id);
    Task<PurchaseOrderDto> ReopenAsync(Guid id);
    Task<PurchaseOrderDto> CloseItemAsync(Guid id, Guid itemId);
    Task<PurchaseOrderDto> ReopenItemAsync(Guid id, Guid itemId);
    Task<PurchaseOrderDto> AmendAsync(Guid id);
    Task<PurchaseOrderDto> UpdateAsync(Guid id, CreatePurchaseOrderDto input);
    Task<UpdateOrderItemsResultDto> UpdateItemsAsync(Guid id, UpdateOrderItemsDto input);
    Task DeleteAsync(Guid id);
    Task<List<OrderPaymentDto>> GetOrderPaymentsAsync(Guid id);
    Task<List<OrderReceiptDto>> GetOrderReceiptsAsync(Guid id);
    Task<PurchaseOrderDto> UpdateDropShipDeliveredQtyAsync(Guid id, UpdateDropShipDeliveredQtyDto input);
    Task<PurchaseOrderDto> RecordSupplierConfirmationAsync(Guid id, RecordSupplierConfirmationDto input);
    Task<List<PendingMaterialRequestItemDto>> GetPendingMaterialRequestItemsAsync(Guid? companyId = null, Guid? supplierId = null);
    Task<PurchaseOrderTrackingBoardDto> GetTrackingBoardAsync(Guid companyId);
}
