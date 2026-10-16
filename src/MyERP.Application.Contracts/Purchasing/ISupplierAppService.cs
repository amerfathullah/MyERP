using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    Task<SupplierOverviewDto> GetSupplierOverviewAsync(GetSupplierOverviewInputDto input);
    Task<List<SupplierTransactionDto>> GetSupplierTransactionsAsync(GetSupplierTransactionsInputDto input);
    Task<List<Guid>> GetSupplierCompanyIdsAsync(Guid supplierId);
}
