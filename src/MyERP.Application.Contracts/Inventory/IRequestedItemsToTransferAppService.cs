using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IRequestedItemsToTransferAppService : IApplicationService
{
    Task<RequestedItemsToTransferReportDto> GetReportAsync(RequestedItemsToTransferFilterDto input);
}
