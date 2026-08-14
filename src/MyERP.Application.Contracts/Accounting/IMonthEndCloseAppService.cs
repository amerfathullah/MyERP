using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IMonthEndCloseAppService : IApplicationService
{
    Task<MonthEndReadinessDto> ValidateReadinessAsync(MonthEndCloseRequestDto input);
    Task<MonthEndCloseStatusDto> GetCloseStatusAsync(MonthEndCloseRequestDto input);
    Task FreezeAsync(FreezeAccountingPeriodDto input);
}
