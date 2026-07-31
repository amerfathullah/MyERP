using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockLedgerAppService : IApplicationService
{
    Task<StockLedgerReportDto> GetStockLedgerAsync(StockLedgerRequestDto input);
    Task<ItemMovementHistoryDto> GetItemMovementHistoryAsync(Guid itemId, Guid? warehouseId = null, int maxEntries = 20);
}
