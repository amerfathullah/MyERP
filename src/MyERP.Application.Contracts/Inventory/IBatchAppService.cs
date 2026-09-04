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

    /// <summary>Returns available batch stock filtered by company, item, and warehouse.</summary>
    Task<System.Collections.Generic.List<AvailableBatchItemDto>> GetAvailableBatchesAsync(GetAvailableBatchesDto input);

    /// <summary>
    /// Returns the first batch in FIFO/expiry order that can cover the full required quantity.
    /// Returns null if no single batch covers the full quantity (allowing serial/batch bundles to split across batches).
    /// Per ERPNext commits 199cae9496 and 9261c9b47f.
    /// </summary>
    Task<AvailableBatchItemDto?> GetBatchCoveringQuantityAsync(AutoPickBatchDto input);

    /// <summary>
    /// Returns the hierarchical tree of batches split from parent batches.
    /// Per ERPNext PR #58530 (Batch Split Tree report).
    /// </summary>
    Task<System.Collections.Generic.List<BatchSplitTreeNodeDto>> GetBatchSplitTreeAsync(GetBatchSplitTreeDto input);
}
