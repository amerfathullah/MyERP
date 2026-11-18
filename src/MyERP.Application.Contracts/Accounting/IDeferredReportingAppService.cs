using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IDeferredReportingAppService : IApplicationService
{
    Task<DeferredRevenueAndExpenseReportDto> GetDeferredRevenueAndExpenseAsync(DeferredRevenueAndExpenseRequestDto input);
}
