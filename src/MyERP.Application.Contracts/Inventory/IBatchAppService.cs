using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IBatchAppService : IApplicationService
{
    Task<BatchDto> GetAsync(Guid id);
    Task<PagedResultDto<BatchDto>> GetListAsync(GetBatchListDto input);
    Task<BatchDto> CreateAsync(CreateBatchDto input);
    Task DisableAsync(Guid id);
    Task<BatchStockBalanceDto> GetStockBalanceAsync(Guid batchId);
    Task<BatchTraceabilityDto> GetTraceabilityAsync(Guid batchId);
    Task<BatchMovementHistoryDto> GetMovementHistoryAsync(Guid batchId, int maxEntries = 50);
}
