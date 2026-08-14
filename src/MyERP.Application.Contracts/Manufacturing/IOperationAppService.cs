using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IOperationAppService : IApplicationService
{
    Task<PagedResultDto<OperationDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<OperationDto> CreateAsync(CreateOperationDto input);
}
