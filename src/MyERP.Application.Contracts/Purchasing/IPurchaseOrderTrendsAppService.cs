using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IPurchaseOrderTrendsAppService : IApplicationService
{
    Task<PurchaseOrderTrendsReportDto> GetReportAsync(PurchaseOrderTrendsRequestDto input);
}
