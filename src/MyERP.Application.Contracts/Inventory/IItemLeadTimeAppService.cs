using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemLeadTimeAppService : IApplicationService
{
    Task<ItemLeadTimeDto> GetAsync(Guid id);
    Task<ItemLeadTimeDto?> GetByItemIdAsync(Guid itemId);
    Task<PagedResultDto<ItemLeadTimeDto>> GetListAsync(GetItemLeadTimeListDto input);
    Task<ItemLeadTimeDto> CreateAsync(CreateUpdateItemLeadTimeDto input);
    Task<ItemLeadTimeDto> UpdateAsync(Guid id, CreateUpdateItemLeadTimeDto input);
    Task DeleteAsync(Guid id);
}
