using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface ISalaryStructureAppService : IApplicationService
{
    Task<PagedResultDto<SalaryStructureDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<SalaryStructureDto> GetAsync(Guid id);
    Task<SalaryStructureDto> CreateAsync(CreateSalaryStructureDto input);
    Task<SalaryStructureDto> UpdateAsync(Guid id, CreateSalaryStructureDto input);
}
