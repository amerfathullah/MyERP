using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockBalanceAppService : IApplicationService
{
    Task<PagedResultDto<StockBalanceDto>> GetStockBalanceAsync(GetStockBalanceRequestDto input);
    Task<List<StockBalanceDto>> GetItemStockAsync(Guid itemId);
    Task<List<ItemAvailabilityDto>> GetItemsAvailabilityAsync(GetItemsAvailabilityInput input);
    Task<BatchWiseBalanceReportDto> GetBatchWiseBalanceAsync(GetBatchWiseBalanceRequestDto input);

    /// <summary>
    /// Recalculates the stock quantities in the Bin for the specified item and warehouse from source ledger entries.
    /// Per ERPNext PR #47125 / commit 36081413d8: provision to recalculate the qty in the Bin.
    /// </summary>
    Task RecalculateBinQtyAsync(Guid itemId, Guid warehouseId);
}
