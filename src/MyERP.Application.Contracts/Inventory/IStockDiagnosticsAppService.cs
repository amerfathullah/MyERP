using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IStockDiagnosticsAppService : IApplicationService
{
    Task<IncorrectBalanceQtyReportDto> GetIncorrectBalanceQtyAsync(IncorrectBalanceQtyRequestDto input);
    Task<IncorrectStockValueReportDto> GetIncorrectStockValueAsync(IncorrectStockValueRequestDto input);
    Task<IncorrectSerialNoValuationReportDto> GetIncorrectSerialNoValuationAsync(IncorrectSerialNoValuationRequestDto input);
    Task<IncorrectSerialAndBatchBundleReportDto> GetIncorrectSerialAndBatchBundlesAsync(IncorrectSerialAndBatchBundleRequestDto input);
    Task<FixSerialAndBatchBundlesResultDto> FixIncorrectSerialAndBatchBundlesAsync(FixSerialAndBatchBundlesInputDto input);
    Task<FifoQueueVsQtyComparisonReportDto> GetFifoQueueVsQtyComparisonAsync(FifoQueueVsQtyComparisonRequestDto input);
    Task<StockValuationComparisonReportDto> GetStockValuationComparisonAsync(StockValuationComparisonRequestDto input);
    Task<List<StockValuationComparisonRepostPreviewRowDto>> GetStockValuationRepostPreviewAsync(StockValuationComparisonRepostRequestDto input);
    Task<StockValuationComparisonRepostResultDto> CreateStockValuationRepostsAsync(StockValuationComparisonRepostRequestDto input);
    Task<StockQtyVsBatchQtyReportDto> GetStockQtyVsBatchQtyAsync(StockQtyVsBatchQtyRequestDto input);
    Task<ReconcileBatchStockResultDto> ReconcileBatchStockQtyAsync(ReconcileBatchStockInputDto input);
}
