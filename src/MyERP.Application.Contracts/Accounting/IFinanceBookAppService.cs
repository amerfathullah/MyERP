using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IFinanceBookAppService : IApplicationService
{
    Task<FinanceBookDto> GetAsync(Guid id);
    Task<PagedResultDto<FinanceBookDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<FinanceBookDto> CreateAsync(CreateFinanceBookDto input);
    Task<FinanceBookDto> SetDefaultAsync(Guid id);
    Task DeleteAsync(Guid id);
}
