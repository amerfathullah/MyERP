using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface IExpenseClaimAppService : IApplicationService
{
    Task<PagedResultDto<ExpenseClaimDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<ExpenseClaimDto> GetAsync(Guid id);
    Task<ExpenseClaimDto> CreateAsync(CreateExpenseClaimDto input);
    Task<ExpenseClaimDto> ApproveAsync(Guid id);
    Task<ExpenseClaimDto> SubmitAsync(Guid id);
    Task<ExpenseClaimDto> RejectAsync(Guid id);
    Task<ExpenseClaimDto> CancelAsync(Guid id);
    Task<Guid> ReimburseAsync(Guid id, Guid paidFromAccountId);
}
