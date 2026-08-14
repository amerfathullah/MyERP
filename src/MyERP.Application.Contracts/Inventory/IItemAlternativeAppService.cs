using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemAlternativeAppService :
    ICrudAppService<
        ItemAlternativeDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateItemAlternativeDto>
{
    Task<List<ItemAlternativeDto>> GetAlternativesAsync(Guid itemId);
}
