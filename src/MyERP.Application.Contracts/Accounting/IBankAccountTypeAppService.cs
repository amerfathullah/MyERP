using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IBankAccountTypeAppService : IApplicationService
{
    Task<BankAccountTypeDto> GetAsync(Guid id);
    Task<PagedResultDto<BankAccountTypeDto>> GetListAsync(GetBankAccountTypeListDto input);
    Task<BankAccountTypeDto> CreateAsync(CreateUpdateBankAccountTypeDto input);
    Task<BankAccountTypeDto> UpdateAsync(Guid id, CreateUpdateBankAccountTypeDto input);
    Task DeleteAsync(Guid id);
}
