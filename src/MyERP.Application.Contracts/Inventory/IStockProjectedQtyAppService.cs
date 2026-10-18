using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockProjectedQtyAppService : IApplicationService
{
    Task<StockProjectedQtyReportDto> GetReportAsync(StockProjectedQtyFilterDto input);
}
