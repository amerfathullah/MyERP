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
    /// Stock Balance Report — shows opening, inward, outward, and closing quantities and values.
    /// Incorporates inventory dimension handling and applies reconciliations against total warehouse balance
    /// to prevent dimension double-counting per ERPNext PR #60698 / commit c8eb481d32.
    /// </summary>
    Task<StockBalanceReportDto> GetStockBalanceReportAsync(GetStockBalanceReportRequestDto input);

    /// <summary>
    /// Recalculates the stock quantities in the Bin for the specified item and warehouse from source ledger entries.
    /// Per ERPNext PR #47125 / commit 36081413d8: provision to recalculate the qty in the Bin.
    /// </summary>
    Task RecalculateBinQtyAsync(Guid itemId, Guid warehouseId);

    /// <summary>
    /// Product Bundle Balance Report — calculates available product bundle quantities based on component stock.
    /// Bundle qty is never negative per ERPNext PR #60627 (max(0, actual_qty // minimum_qty)).
    /// </summary>
    Task<ProductBundleBalanceReportDto> GetProductBundleBalanceAsync(GetProductBundleBalanceRequestDto input);
}
