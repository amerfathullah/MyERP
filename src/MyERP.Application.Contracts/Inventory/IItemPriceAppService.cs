using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemPriceAppService : IApplicationService
{
    Task<PagedResultDto<ItemPriceDto>> GetListAsync(GetItemPriceListDto input);
    Task<ItemPriceDto> GetAsync(Guid id);
    Task<ItemPriceDto> CreateAsync(CreateUpdateItemPriceDto input);
    Task<ItemPriceDto> UpdateAsync(Guid id, CreateUpdateItemPriceDto input);
    Task DeleteAsync(Guid id);
    Task<BulkPriceUpdateResultDto> BulkUpdateAsync(BulkPriceUpdateDto input);
}
