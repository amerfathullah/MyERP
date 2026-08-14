using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesPersonAppService : IApplicationService
{
    Task<SalesPersonDto> GetAsync(Guid id);
    Task<PagedResultDto<SalesPersonDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<List<SalesPersonDto>> GetTreeAsync();
    Task<SalesPersonDto> CreateAsync(CreateSalesPersonDto input);
    Task<SalesPersonDto> UpdateAsync(Guid id, UpdateSalesPersonDto input);
    Task AddTargetAsync(Guid id, CreateSalesTargetDto input);
    Task DisableAsync(Guid id);
    Task DeleteAsync(Guid id);
}
