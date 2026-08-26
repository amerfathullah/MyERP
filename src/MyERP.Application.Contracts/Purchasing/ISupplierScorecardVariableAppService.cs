using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface ISupplierScorecardVariableAppService : IApplicationService
{
    Task<SupplierScorecardVariableDto> GetAsync(Guid id);
    Task<PagedResultDto<SupplierScorecardVariableDto>> GetListAsync(GetSupplierScorecardVariableListDto input);
    Task<List<SupplierScorecardVariableDto>> GetAllListAsync();
    Task<SupplierScorecardVariableDto> CreateAsync(CreateUpdateSupplierScorecardVariableDto input);
    Task<SupplierScorecardVariableDto> UpdateAsync(Guid id, CreateUpdateSupplierScorecardVariableDto input);
    Task DeleteAsync(Guid id);
}
