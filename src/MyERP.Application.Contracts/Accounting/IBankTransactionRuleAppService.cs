using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IBankTransactionRuleAppService : IApplicationService
{
    Task<PagedResultDto<BankTransactionRuleDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<BankTransactionRuleDto> CreateAsync(CreateBankTransactionRuleDto input);
    Task DisableAsync(Guid id);
    Task EnableAsync(Guid id);
    Task<AutoMatchResultDto> EvaluateRulesAsync(EvaluateRulesDto input);
    Task<int> GetNextPriorityAsync(Guid companyId);
    Task ReorderPrioritiesAsync(Guid companyId);
}
