using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemAppService :
    ICrudAppService<
        ItemDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateItemDto>
{
}
