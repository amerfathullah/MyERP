using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockLedgerAppService : IApplicationService
{
    Task<StockLedgerReportDto> GetStockLedgerAsync(StockLedgerRequestDto input);
}
