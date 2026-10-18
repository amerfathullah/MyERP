using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockLedgerEntriesOfCancelledVouchersAppService : IApplicationService
{
    Task<StockLedgerEntriesOfCancelledVouchersReportDto> GetReportAsync(StockLedgerEntriesOfCancelledVouchersFilterDto input);
    Task<FixUncancelledEntriesResultDto> FixUncancelledEntriesAsync(FixUncancelledEntriesInputDto input);
}
