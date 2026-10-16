using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IPendingSoItemsForPurchaseRequestAppService : IApplicationService
{
    Task<PendingSoItemsForPurchaseRequestReportDto> GetReportAsync(PendingSoItemsForPurchaseRequestRequestDto input);
}
