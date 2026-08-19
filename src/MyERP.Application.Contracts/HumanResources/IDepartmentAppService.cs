using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface IDepartmentAppService : IApplicationService
{
    Task<PagedResultDto<DepartmentDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<DepartmentDto> GetAsync(Guid id);
    Task<DepartmentDto> CreateAsync(CreateUpdateDepartmentDto input);
    Task<DepartmentDto> UpdateAsync(Guid id, CreateUpdateDepartmentDto input);
    Task DeleteAsync(Guid id);
}
