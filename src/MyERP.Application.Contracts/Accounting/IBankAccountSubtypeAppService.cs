using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IBankAccountSubtypeAppService : IApplicationService
{
    Task<BankAccountSubtypeDto> GetAsync(Guid id);
    Task<PagedResultDto<BankAccountSubtypeDto>> GetListAsync(GetBankAccountSubtypeListDto input);
    Task<BankAccountSubtypeDto> CreateAsync(CreateUpdateBankAccountSubtypeDto input);
    Task<BankAccountSubtypeDto> UpdateAsync(Guid id, CreateUpdateBankAccountSubtypeDto input);
    Task DeleteAsync(Guid id);
}
