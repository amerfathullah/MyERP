using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IBilledItemsToBeReceivedAppService : IApplicationService
{
    Task<BilledItemsToBeReceivedReportDto> GetReportAsync(GetBilledItemsToBeReceivedInput input);
}
