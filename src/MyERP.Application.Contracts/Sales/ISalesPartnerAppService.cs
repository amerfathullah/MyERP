using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesPartnerAppService : IApplicationService
{
    Task<PagedResultDto<SalesPartnerDto>> GetListAsync(GetSalesPartnerListDto input);
    Task<SalesPartnerDto> GetAsync(Guid id);
    Task<SalesPartnerDto> CreateAsync(CreateSalesPartnerDto input);
    Task<SalesPartnerDto> UpdateAsync(Guid id, CreateSalesPartnerDto input);
    Task DeleteAsync(Guid id);
    Task ToggleAsync(Guid id);
}
