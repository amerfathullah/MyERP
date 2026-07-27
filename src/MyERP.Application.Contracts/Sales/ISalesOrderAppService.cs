using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    Task DeleteAsync(Guid id);
    Task<List<DeliveryScheduleEntryDto>> GetDeliveryScheduleAsync(Guid orderId);
}
