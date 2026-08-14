using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface ICashFlowForecastAppService : IApplicationService
{
    Task<CashFlowForecastDto> GetForecastAsync(CashFlowForecastRequestDto input);
}
