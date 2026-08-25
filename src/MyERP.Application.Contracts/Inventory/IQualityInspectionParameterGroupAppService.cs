using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IQualityInspectionParameterGroupAppService : IApplicationService
{
    Task<QualityInspectionParameterGroupDto> GetAsync(Guid id);
    Task<PagedResultDto<QualityInspectionParameterGroupDto>> GetListAsync(GetQualityInspectionParameterGroupListDto input);
    Task<List<QualityInspectionParameterGroupDto>> GetAllListAsync();
    Task<QualityInspectionParameterGroupDto> CreateAsync(CreateUpdateQualityInspectionParameterGroupDto input);
    Task<QualityInspectionParameterGroupDto> UpdateAsync(Guid id, CreateUpdateQualityInspectionParameterGroupDto input);
    Task DeleteAsync(Guid id);
}
