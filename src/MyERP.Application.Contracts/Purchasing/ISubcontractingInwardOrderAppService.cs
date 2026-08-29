using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface ISubcontractingInwardOrderAppService : IApplicationService
{
    Task<PagedResultDto<SubcontractingInwardOrderDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<SubcontractingInwardOrderDto> GetAsync(Guid id);
    Task<SubcontractingInwardOrderDto> CreateAsync(CreateSubcontractingInwardOrderDto input);
    Task<SubcontractingInwardOrderDto> SubmitAsync(Guid id);
    Task<SubcontractingInwardOrderDto> CancelAsync(Guid id);
    Task<SubcontractingInwardOrderDto> CloseAsync(Guid id);
    Task<SubcontractingInwardOrderDto> ReopenAsync(Guid id);
    Task<CreateSubcontractingInwardOrderDto> MapFromSalesOrderAsync(MapSubcontractingInwardOrderFromSalesOrderDto input);
    Task<SubcontractingInwardOrderActionSummaryDto> GetActionSummaryAsync(Guid id);
    Task<SubcontractingInwardOrderDto> ReceiveItemsAsync(Guid id, ScioReceiveItemsDto input);
}
