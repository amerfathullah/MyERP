using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IInventoryAgingAppService : IApplicationService
{
    Task<InventoryAgingReportDto> GetReportAsync(InventoryAgingRequestDto input);
}
