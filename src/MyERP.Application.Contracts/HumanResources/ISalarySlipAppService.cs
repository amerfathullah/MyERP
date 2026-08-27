using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface ISalarySlipAppService : IApplicationService
{
    Task<PagedResultDto<SalarySlipDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<SalarySlipDto> GetAsync(Guid id);
    Task<SalarySlipDto> CreateAsync(CreateSalarySlipDto input);
    Task<SalarySlipDto> UpdateAsync(Guid id, CreateSalarySlipDto input);
    Task DeleteAsync(Guid id);
    Task<SalarySlipDto> SubmitAsync(Guid id);
    Task<SalarySlipDto> CancelAsync(Guid id);
}
