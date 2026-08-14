using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IFiscalYearAppService : IApplicationService
{
    Task<PagedResultDto<FiscalYearDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<FiscalYearDto> GetAsync(Guid id);
    Task<FiscalYearDto> GetCurrentAsync(Guid companyId);
    Task<FiscalYearDto> CreateAsync(CreateFiscalYearDto input);
    Task<FiscalYearDto> CloseAsync(Guid id);
}
