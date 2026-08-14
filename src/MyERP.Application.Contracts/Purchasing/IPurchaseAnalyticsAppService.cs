using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IPurchaseAnalyticsAppService : IApplicationService
{
    Task<PurchaseAnalyticsReportDto> GetReportAsync(PurchaseAnalyticsRequestDto input);
}
