using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface ICostCenterAppService : IApplicationService
{
    Task<PagedResultDto<CostCenterDto>> GetListAsync(GetCostCenterListDto input);
    Task<CostCenterDto> CreateAsync(CreateCostCenterDto input);
    Task<CostCenterDto> UpdateAsync(Guid id, CreateCostCenterDto input);
    Task<System.Collections.Generic.List<CostCenterTreeNodeDto>> GetTreeAsync(Guid companyId, bool includeDisabled = false);
}
