using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemManufacturerAppService :
    ICrudAppService<
        ItemManufacturerDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateItemManufacturerDto>
{
    Task<List<ItemManufacturerDto>> GetListByItemAsync(Guid itemId);
}
