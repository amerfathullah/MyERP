using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemGroupAppService : IApplicationService
{
    Task<PagedResultDto<ItemGroupDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<ItemGroupDto> CreateAsync(CreateItemGroupDto input);
}
