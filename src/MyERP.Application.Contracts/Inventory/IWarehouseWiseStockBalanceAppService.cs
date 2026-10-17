using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IWarehouseWiseStockBalanceAppService : IApplicationService
{
    Task<WarehouseWiseStockBalanceReportDto> GetReportAsync(WarehouseWiseStockBalanceFilterDto input);
}
