using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface ISupplierAppService :
    ICrudAppService<
        SupplierDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateSupplierDto>
{
}
