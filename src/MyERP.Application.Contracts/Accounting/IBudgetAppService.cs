using System;
using System.Threading.Tasks;
using MyERP.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IBudgetAppService : IApplicationService
{
    Task<PagedResultDto<BudgetDto>> GetListAsync(GetBudgetListDto input);
    Task<BudgetDto> GetAsync(Guid id);
    Task<BudgetDto> CreateAsync(CreateBudgetDto input);
    Task<BudgetDto> SubmitAsync(Guid id);
    Task<BudgetDto> CancelAsync(Guid id);
}
