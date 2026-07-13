using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesOrderAppService : IApplicationService
{
    Task<SalesOrderDto> GetAsync(Guid id);
    Task<PagedResultDto<SalesOrderDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<SalesOrderDto> CreateAsync(CreateSalesOrderDto input);
    Task<SalesOrderDto> SubmitAsync(Guid id);
    Task<SalesOrderDto> CancelAsync(Guid id);
}
