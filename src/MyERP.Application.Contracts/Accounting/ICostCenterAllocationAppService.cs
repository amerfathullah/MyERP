using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface ICostCenterAllocationAppService : IApplicationService
{
    Task<PagedResultDto<CostCenterAllocationDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<CostCenterAllocationDto> GetAsync(Guid id);
    Task<CostCenterAllocationDto> CreateAsync(CreateCostCenterAllocationDto input);
    Task ToggleActiveAsync(Guid id);
    Task DeleteAsync(Guid id);
}
