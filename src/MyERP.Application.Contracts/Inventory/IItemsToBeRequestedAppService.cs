using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

/// <summary>
/// Items To Be Requested Report service.
/// Identifies stock balances where projected qty is negative and procurement requisitions are needed.
/// Ported from ERPNext stock/report/items_to_be_requested.
/// </summary>
public interface IItemsToBeRequestedAppService : IApplicationService
{
    Task<ItemsToBeRequestedReportDto> GetReportAsync(ItemsToBeRequestedFilterDto input);
}
