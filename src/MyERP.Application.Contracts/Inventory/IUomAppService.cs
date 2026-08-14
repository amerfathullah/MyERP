using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IUomAppService : IApplicationService
{
    Task<PagedResultDto<UomDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<UomDto> GetAsync(Guid id);
    Task<UomDto> CreateAsync(CreateUomDto input);
    Task DeleteAsync(Guid id);
}
