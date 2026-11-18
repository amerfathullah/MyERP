import type {
  FifoQueueVsQtyComparisonReportDto,
  FifoQueueVsQtyComparisonRequestDto,
  FixSerialAndBatchBundlesInputDto,
  FixSerialAndBatchBundlesResultDto,
  IncorrectBalanceQtyReportDto,
  IncorrectBalanceQtyRequestDto,
  IncorrectSerialAndBatchBundleReportDto,
  IncorrectSerialAndBatchBundleRequestDto,
  IncorrectSerialNoValuationReportDto,
  IncorrectSerialNoValuationRequestDto,
  IncorrectStockValueReportDto,
  IncorrectStockValueRequestDto,
  StockValuationComparisonReportDto,
  StockValuationComparisonRepostPreviewRowDto,
  StockValuationComparisonRepostRequestDto,
  StockValuationComparisonRepostResultDto,
  StockValuationComparisonRequestDto,
  StockQtyVsBatchQtyReportDto,
  StockQtyVsBatchQtyRequestDto,
  ReconcileBatchStockInputDto,
  ReconcileBatchStockResultDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockDiagnosticsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getIncorrectBalanceQty = (
    input: IncorrectBalanceQtyRequestDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, IncorrectBalanceQtyReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/incorrect-balance-qty',
        params: {
          companyId: input.companyId,
          warehouseId: input.warehouseId,
          itemId: input.itemId,
          precision: input.precision,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getIncorrectStockValue = (
    input: IncorrectStockValueRequestDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, IncorrectStockValueReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/incorrect-stock-value',
        params: {
          companyId: input.companyId,
          accountId: input.accountId,
          fromDate: input.fromDate,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getIncorrectSerialNoValuation = (
    input: IncorrectSerialNoValuationRequestDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, IncorrectSerialNoValuationReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/incorrect-serial-no-valuation',
        params: {
          companyId: input.companyId,
          itemId: input.itemId,
          fromDate: input.fromDate,
          toDate: input.toDate,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getIncorrectSerialAndBatchBundles = (
    input: IncorrectSerialAndBatchBundleRequestDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, IncorrectSerialAndBatchBundleReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/incorrect-serial-and-batch-bundles',
        params: {
          companyId: input.companyId,
          itemId: input.itemId,
          warehouseId: input.warehouseId,
          voucherType: input.voucherType,
          postingDate: input.postingDate,
        },
      },
      { apiName: this.apiName, ...config },
    );

  fixIncorrectSerialAndBatchBundles = (
    input: FixSerialAndBatchBundlesInputDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, FixSerialAndBatchBundlesResultDto>(
      {
        method: 'POST',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/fix-incorrect-serial-and-batch-bundles',
        body: input,
      },
      { apiName: this.apiName, ...config },
    );

  getFifoQueueVsQtyComparison = (
    input: FifoQueueVsQtyComparisonRequestDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, FifoQueueVsQtyComparisonReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/fifo-queue-vs-qty-comparison',
        params: {
          companyId: input.companyId,
          itemId: input.itemId,
          itemGroupId: input.itemGroupId,
          warehouseId: input.warehouseId,
          fromDate: input.fromDate,
          toDate: input.toDate,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getStockValuationComparison = (
    input: StockValuationComparisonRequestDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, StockValuationComparisonReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/stock-valuation-comparison',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          itemId: input.itemId,
          itemGroupId: input.itemGroupId,
          warehouseId: input.warehouseId,
          show: input.show,
          tolerance: input.tolerance,
          showAdjustedDifferences: input.showAdjustedDifferences,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getStockValuationRepostPreview = (
    input: StockValuationComparisonRepostRequestDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, StockValuationComparisonRepostPreviewRowDto[]>(
      {
        method: 'POST',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/stock-valuation-repost-preview',
        body: input,
      },
      { apiName: this.apiName, ...config },
    );

  createStockValuationReposts = (
    input: StockValuationComparisonRepostRequestDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, StockValuationComparisonRepostResultDto>(
      {
        method: 'POST',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/create-stock-valuation-reposts',
        body: input,
      },
      { apiName: this.apiName, ...config },
    );

  getStockQtyVsBatchQty = (
    input: StockQtyVsBatchQtyRequestDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, StockQtyVsBatchQtyReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/stock-qty-vs-batch-qty',
        params: {
          companyId: input.companyId,
          warehouseId: input.warehouseId,
          itemId: input.itemId,
          batchId: input.batchId,
          showDisabledItems: input.showDisabledItems,
          showOnlyDifferences: input.showOnlyDifferences,
          searchTerm: input.searchTerm,
        },
      },
      { apiName: this.apiName, ...config },
    );

  reconcileBatchStockQty = (
    input: ReconcileBatchStockInputDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, ReconcileBatchStockResultDto>(
      {
        method: 'POST',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/reconcile-batch-stock-qty',
        body: input,
      },
      { apiName: this.apiName, ...config },
    );
}
