using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockQtyVsSerialNoCountAppService : IApplicationService
{
    Task<StockQtyVsSerialNoCountReportDto> GetReportAsync(StockQtyVsSerialNoCountFilterDto input);
    Task<SyncSerialNoStatusResultDto> SyncSerialNoStatusAsync(SyncSerialNoStatusInputDto input);
}
