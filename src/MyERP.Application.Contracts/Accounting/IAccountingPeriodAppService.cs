using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IAccountingPeriodAppService : IApplicationService
{
    Task<PagedResultDto<AccountingPeriodDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<AccountingPeriodDto> CloseAsync(Guid id);
}
