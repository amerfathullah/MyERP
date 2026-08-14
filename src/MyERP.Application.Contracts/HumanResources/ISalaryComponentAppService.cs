using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface ISalaryComponentAppService : IApplicationService
{
    Task<PagedResultDto<SalaryComponentDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<SalaryComponentDto> GetAsync(Guid id);
    Task<SalaryComponentDto> CreateAsync(CreateUpdateSalaryComponentDto input);
    Task<SalaryComponentDto> UpdateAsync(Guid id, CreateUpdateSalaryComponentDto input);
    Task DeleteAsync(Guid id);
}
