using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesPartnerTypeAppService : IApplicationService
{
    Task<SalesPartnerTypeDto> GetAsync(Guid id);
    Task<PagedResultDto<SalesPartnerTypeDto>> GetListAsync(GetSalesPartnerTypeListDto input);
    Task<SalesPartnerTypeDto> CreateAsync(CreateUpdateSalesPartnerTypeDto input);
    Task<SalesPartnerTypeDto> UpdateAsync(Guid id, CreateUpdateSalesPartnerTypeDto input);
    Task DeleteAsync(Guid id);
}
