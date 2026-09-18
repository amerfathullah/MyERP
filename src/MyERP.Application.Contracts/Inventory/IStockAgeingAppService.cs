using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockAgeingAppService : IApplicationService
{
    Task<StockAgeingReportDto> GetReportAsync(StockAgeingFilterDto input);
}
