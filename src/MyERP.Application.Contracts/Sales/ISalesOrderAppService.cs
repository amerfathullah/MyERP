using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesOrderAppService : IApplicationService
{
    Task<SalesOrderDto> GetAsync(Guid id);
    Task<PagedResultDto<SalesOrderDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<SalesOrderDto> CreateAsync(CreateSalesOrderDto input);
    Task<SalesOrderDto> SubmitAsync(Guid id);
    Task<SalesOrderDto> CancelAsync(Guid id);
}
