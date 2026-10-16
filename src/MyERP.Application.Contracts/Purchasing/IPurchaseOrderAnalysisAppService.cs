using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IPurchaseOrderAnalysisAppService : IApplicationService
{
    Task<PurchaseOrderAnalysisReportDto> GetAnalysisAsync(GetPurchaseOrderAnalysisDto input);
}
