using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IRequestedItemsToOrderAndReceiveAppService : IApplicationService
{
    Task<RequestedItemsToOrderAndReceiveReportDto> GetReportAsync(RequestedItemsFilterDto input);
}
