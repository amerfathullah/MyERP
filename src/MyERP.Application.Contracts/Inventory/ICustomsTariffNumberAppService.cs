using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface ICustomsTariffNumberAppService : ICrudAppService<
    CustomsTariffNumberDto,
    Guid,
    PagedAndSortedResultRequestDto,
    CreateUpdateCustomsTariffNumberDto,
    CreateUpdateCustomsTariffNumberDto>
{
}
