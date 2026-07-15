using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public class GetSupplierListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}

public interface ISupplierAppService :
    ICrudAppService<
        SupplierDto,
        Guid,
        GetSupplierListDto,
        CreateUpdateSupplierDto>
{
}
