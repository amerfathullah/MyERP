using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public class GetItemListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? CompanyId { get; set; }
}

public interface IItemAppService :
    ICrudAppService<
        ItemDto,
        Guid,
        GetItemListDto,
        CreateUpdateItemDto>
{
}
