using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IBankGuaranteeAppService : ICrudAppService<
    BankGuaranteeDto,
    Guid,
    PagedAndSortedResultRequestDto,
    CreateUpdateBankGuaranteeDto,
    CreateUpdateBankGuaranteeDto>
{
    Task<BankGuaranteeDto> SubmitAsync(Guid id);
    Task<BankGuaranteeDto> CancelAsync(Guid id);
}
