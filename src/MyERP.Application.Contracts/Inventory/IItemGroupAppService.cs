using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemGroupAppService : IApplicationService
{
    Task<PagedResultDto<ItemGroupDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<ItemGroupDto> GetAsync(Guid id);
    Task<ItemGroupDto> CreateAsync(CreateItemGroupDto input);
    Task<ItemGroupDto> UpdateAsync(Guid id, CreateItemGroupDto input);
    Task DeleteAsync(Guid id);
}
