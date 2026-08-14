using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemStandardCostAppService : IApplicationService
{
    Task<PagedResultDto<ItemStandardCostDto>> GetListAsync(GetItemStandardCostListDto input);
    Task<ItemStandardCostDto> GetAsync(Guid id);
    Task<ItemStandardCostDto?> GetCurrentAsync(Guid itemId, Guid companyId);
    Task<ItemStandardCostDto> CreateAsync(CreateItemStandardCostDto input);
    Task<ItemStandardCostDto> SubmitAsync(Guid id);
    Task<ItemStandardCostDto> CancelAsync(Guid id);
}
