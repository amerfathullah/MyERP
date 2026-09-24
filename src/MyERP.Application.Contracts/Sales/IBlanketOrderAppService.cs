using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IBlanketOrderAppService : IApplicationService
{
    Task<PagedResultDto<BlanketOrderDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<BlanketOrderDto> GetAsync(Guid id);
    Task<BlanketOrderDto> CreateAsync(CreateBlanketOrderDto input);
    Task<BlanketOrderDto> SubmitAsync(Guid id);
    Task<BlanketOrderDto> CancelAsync(Guid id);
    Task<BlanketOrderDto> CloseAsync(Guid id);
    Task<BlanketOrderDto> ReopenAsync(Guid id);
    Task<BlanketOrderDto> CloseItemAsync(Guid id, Guid itemId);
    Task<BlanketOrderDto> ReopenItemAsync(Guid id, Guid itemId);
}
