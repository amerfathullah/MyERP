using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IDelayedOrderReportAppService : IApplicationService
{
    Task<DelayedOrderReportDto> GetReportAsync(DelayedOrderFilterDto input);
}
