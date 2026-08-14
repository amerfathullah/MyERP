using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface ISubcontractingAppService : IApplicationService
{
    // Subcontracting Order
    Task<SubcontractingOrderDto> GetOrderAsync(Guid id);
    Task<PagedResultDto<SubcontractingOrderDto>> GetOrderListAsync(GetScoListDto input);
    Task<SubcontractingOrderDto> CreateOrderAsync(CreateSubcontractingOrderDto input);
    Task<SubcontractingOrderDto> SubmitOrderAsync(Guid id);
    Task<SubcontractingOrderDto> CancelOrderAsync(Guid id);
    Task<SubcontractingOrderDto> CloseOrderAsync(Guid id);
    Task<RmTransferResultDto> CreateRmTransferStockEntryAsync(Guid scoId);

    // Subcontracting Receipt
    Task<SubcontractingReceiptDto> CreateReceiptAsync(CreateSubcontractingReceiptDto input);
    Task<SubcontractingReceiptDto> SubmitReceiptAsync(Guid id);
    Task<SubcontractingReceiptDto> CancelReceiptAsync(Guid id);
}
