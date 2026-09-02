using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Purchasing;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesOrderAppService : IApplicationService
{
    Task<SalesOrderDto> GetAsync(Guid id);
    Task<PagedResultDto<SalesOrderDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<SalesOrderDto> CreateAsync(CreateSalesOrderDto input);
    Task<SalesOrderDto> UpdateAsync(Guid id, CreateSalesOrderDto input);
    Task<SalesOrderDto> SubmitAsync(Guid id);
    Task<BulkOperationResultDto> BulkSubmitAsync(List<Guid> ids);
    Task<SalesOrderDto> CancelAsync(Guid id);
    Task<SalesOrderDto> CloseAsync(Guid id);
    Task<SalesOrderDto> ReopenAsync(Guid id);
    Task<SalesOrderDto> CloseItemAsync(Guid id, Guid itemId);
    Task<SalesOrderDto> ReopenItemAsync(Guid id, Guid itemId);
    Task DeleteAsync(Guid id);
    Task<List<DeliveryScheduleEntryDto>> GetDeliveryScheduleAsync(Guid orderId);

    /// <summary>
    /// Updates qty/rate on submitted SO items (post-submit editing).
    /// Per ERPNext update_child_qty_rate: guards against qty below delivered, rate below billed.
    /// </summary>
    Task<UpdateOrderItemsResultDto> UpdateItemsAsync(Guid id, UpdateOrderItemsDto input);
}
