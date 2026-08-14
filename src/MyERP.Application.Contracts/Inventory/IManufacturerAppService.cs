using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IManufacturerAppService :
    ICrudAppService<
        ManufacturerDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateManufacturerDto>
{
}
