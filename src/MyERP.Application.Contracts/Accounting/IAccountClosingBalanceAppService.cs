using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IAccountClosingBalanceAppService : IApplicationService
{
    Task<List<AccountClosingBalanceDto>> GetListAsync(Guid companyId, string period);
    Task<ClosingBalanceStatusDto> GetStatusAsync(Guid companyId);
    Task<int> RebuildAsync(RebuildClosingBalanceDto input);
}
