using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IRoutingAppService : IApplicationService
{
    Task<RoutingDto> GetAsync(Guid id);
    Task<PagedResultDto<RoutingDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<RoutingDto> CreateAsync(CreateRoutingDto input);
    Task<RoutingDto> UpdateAsync(Guid id, CreateRoutingDto input);
    Task DeleteAsync(Guid id);
}
