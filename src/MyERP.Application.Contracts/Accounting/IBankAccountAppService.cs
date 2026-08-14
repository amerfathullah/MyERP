using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IBankAccountAppService : IApplicationService
{
    Task<PagedResultDto<BankAccountDto>> GetListAsync(GetBankAccountListDto input);
    Task<BankAccountDto> GetAsync(Guid id);
    Task<BankAccountDto> CreateAsync(CreateUpdateBankAccountDto input);
    Task<BankAccountDto> UpdateAsync(Guid id, CreateUpdateBankAccountDto input);
    Task<BankAccountDto> SetAsDefaultAsync(Guid id);
    Task<BankAccountDto> DisableAsync(Guid id);
    Task DeleteAsync(Guid id);
}
