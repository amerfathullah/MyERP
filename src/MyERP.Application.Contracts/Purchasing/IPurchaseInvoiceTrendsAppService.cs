using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IPurchaseInvoiceTrendsAppService : IApplicationService
{
    Task<PurchaseInvoiceTrendsReportDto> GetReportAsync(PurchaseInvoiceTrendsRequestDto input);
}
