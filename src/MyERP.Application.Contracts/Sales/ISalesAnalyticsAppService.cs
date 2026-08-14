using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesAnalyticsAppService : IApplicationService
{
    Task<SalesAnalyticsReportDto> GetReportAsync(SalesAnalyticsRequestDto input);
}
