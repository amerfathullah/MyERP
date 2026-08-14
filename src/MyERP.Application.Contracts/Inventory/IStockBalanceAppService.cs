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
}
