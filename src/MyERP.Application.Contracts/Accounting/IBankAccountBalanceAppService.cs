using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IBankAccountBalanceAppService : IApplicationService
{
    Task<BankAccountBalanceDto> GetAsync(Guid id);
    Task<PagedResultDto<BankAccountBalanceDto>> GetListAsync(GetBankAccountBalanceListDto input);
    Task<List<BankAccountBalanceDto>> GetAllListAsync(Guid bankAccountId);
    Task<BankAccountBalanceDto> CreateAsync(CreateUpdateBankAccountBalanceDto input);
    Task<BankAccountBalanceDto> UpdateAsync(Guid id, CreateUpdateBankAccountBalanceDto input);
    Task DeleteAsync(Guid id);
}
