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
}
