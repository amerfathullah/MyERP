using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockLedgerInvariantCheckAppService : IApplicationService
{
    Task<StockLedgerInvariantCheckReportDto> GetInvariantCheckAsync(StockLedgerInvariantCheckFilterDto input);
    Task<StockLedgerVarianceReportDto> GetStockLedgerVarianceAsync(StockLedgerVarianceFilterDto input);
}
