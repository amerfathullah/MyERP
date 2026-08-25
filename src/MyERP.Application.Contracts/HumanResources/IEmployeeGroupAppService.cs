using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface IEmployeeGroupAppService : IApplicationService
{
    Task<EmployeeGroupDto> GetAsync(Guid id);
    Task<PagedResultDto<EmployeeGroupDto>> GetListAsync(GetEmployeeGroupListDto input);
    Task<EmployeeGroupDto> CreateAsync(CreateUpdateEmployeeGroupDto input);
    Task<EmployeeGroupDto> UpdateAsync(Guid id, CreateUpdateEmployeeGroupDto input);
    Task DeleteAsync(Guid id);
}
