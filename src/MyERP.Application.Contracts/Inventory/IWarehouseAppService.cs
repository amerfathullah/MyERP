using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public class GetWarehouseListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}

public interface IWarehouseAppService :
    ICrudAppService<
        WarehouseDto,
        Guid,
        GetWarehouseListDto,
        CreateUpdateWarehouseDto>
{
}
