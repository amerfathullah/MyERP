using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    Task<CustomerOverviewDto> GetCustomerOverviewAsync(GetCustomerOverviewInputDto input);
    Task<System.Collections.Generic.List<CustomerTransactionDto>> GetCustomerTransactionsAsync(GetCustomerTransactionsInputDto input);
    Task<System.Collections.Generic.List<string>> GetCustomerCompaniesAsync(Guid customerId);
}
