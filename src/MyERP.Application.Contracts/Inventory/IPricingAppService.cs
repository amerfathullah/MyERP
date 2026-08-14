using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IPricingAppService : IApplicationService
{
    Task<List<PriceListDto>> GetPriceListsAsync();
    Task<PriceListDto> CreatePriceListAsync(CreateUpdatePriceListDto input);
    Task<PriceListDto> UpdatePriceListAsync(Guid id, CreateUpdatePriceListDto input);
    Task<PagedResultDto<ItemPriceDto>> GetItemPricesAsync(GetItemPriceListDto input);
    Task<ItemPriceDto> CreateItemPriceAsync(CreateUpdateItemPriceDto input);
    Task<ItemPriceDto> UpdateItemPriceAsync(Guid id, CreateUpdateItemPriceDto input);
    Task DeleteItemPriceAsync(Guid id);
    Task<ItemRateResultDto> GetItemRateAsync(GetItemRateRequestDto input);
}
