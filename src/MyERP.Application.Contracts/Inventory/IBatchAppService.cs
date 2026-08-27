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

    /// <summary>Splits a portion of an existing batch into a new batch via Repack Stock Entry.</summary>
    Task<SplitBatchResultDto> SplitBatchAsync(SplitBatchDto input);

    /// <summary>Moves batch stock from source warehouse to target warehouse via Material Transfer Stock Entry.</summary>
    Task<MoveBatchResultDto> MoveBatchAsync(MoveBatchDto input);
}
