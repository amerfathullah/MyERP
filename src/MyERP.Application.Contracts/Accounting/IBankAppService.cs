using System;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IBankAppService : ICrudAppService<BankDto, Guid, GetBankListDto, CreateUpdateBankDto, CreateUpdateBankDto>
{
}
