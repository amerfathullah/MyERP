using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IPriceListAppService :
    ICrudAppService<PriceListDto, Guid, PagedAndSortedResultRequestDto, CreateUpdatePriceListDto>
{
}
