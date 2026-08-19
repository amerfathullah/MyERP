using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IOperationAppService : IApplicationService
{
    Task<OperationDto> GetAsync(Guid id);
    Task<PagedResultDto<OperationDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<OperationDto> CreateAsync(CreateOperationDto input);
    Task<OperationDto> UpdateAsync(Guid id, CreateOperationDto input);
    Task DeleteAsync(Guid id);
}
