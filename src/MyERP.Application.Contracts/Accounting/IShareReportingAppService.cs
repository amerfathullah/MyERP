using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IShareReportingAppService : IApplicationService
{
    Task<ShareBalanceReportDto> GetShareBalanceAsync(ShareBalanceRequestDto input);
    Task<ShareLedgerReportDto> GetShareLedgerAsync(ShareLedgerRequestDto input);
}
