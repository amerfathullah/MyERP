using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IRoutingAppService : IApplicationService
{
    Task<PagedResultDto<RoutingDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<RoutingDto> CreateAsync(CreateRoutingDto input);
}
