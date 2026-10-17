using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IPurchaseReceiptTrendsAppService : IApplicationService
{
    Task<PurchaseReceiptTrendsReportDto> GetReportAsync(PurchaseReceiptTrendsRequestDto input);
}
