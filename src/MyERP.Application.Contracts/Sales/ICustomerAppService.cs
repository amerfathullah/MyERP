using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public class GetCustomerListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}

public interface ICustomerAppService :
    ICrudAppService<
        CustomerDto,
        Guid,
        GetCustomerListDto,
        CreateUpdateCustomerDto>
{
}
