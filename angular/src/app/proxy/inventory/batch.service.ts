import type { AutoPickBatchDto, AvailableBatchItemDto, BatchDto, BatchMovementHistoryDto, BatchSplitTreeNodeDto, BatchStockBalanceDto, BatchTraceabilityDto, CreateBatchDto, GetAvailableBatchesDto, GetBatchListDto, GetBatchSplitTreeDto, MoveBatchDto, MoveBatchResultDto, SplitBatchDto, SplitBatchResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BatchService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateBatchDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchDto>({
      method: 'POST',
      url: '/api/app/batch',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  disable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/batch/${id}/disable`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchDto>({
      method: 'GET',
      url: `/api/app/batch/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAvailableBatches = (input: GetAvailableBatchesDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AvailableBatchItemDto[]>({
      method: 'GET',
      url: '/api/app/batch/available-batches',
      params: { companyId: input.companyId, itemId: input.itemId, warehouseId: input.warehouseId, sameDocumentBatchQuantities: input.sameDocumentBatchQuantities },
    },
    { apiName: this.apiName,...config });
  

  getBatchCoveringQuantity = (input: AutoPickBatchDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AvailableBatchItemDto>({
      method: 'GET',
      url: '/api/app/batch/batch-covering-quantity',
      params: { companyId: input.companyId, itemId: input.itemId, warehouseId: input.warehouseId, requiredStockQty: input.requiredStockQty, sameDocumentBatchQuantities: input.sameDocumentBatchQuantities },
    },
    { apiName: this.apiName,...config });
  

  getBatchSplitTree = (input: GetBatchSplitTreeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchSplitTreeNodeDto[]>({
      method: 'GET',
      url: '/api/app/batch/batch-split-tree',
      params: { batchId: input.batchId, itemId: input.itemId },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetBatchListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BatchDto>>({
      method: 'GET',
      url: '/api/app/batch',
      params: { itemId: input.itemId, isDisabled: input.isDisabled, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMovementHistory = (batchId: string, maxEntries: number = 50, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchMovementHistoryDto>({
      method: 'GET',
      url: `/api/app/batch/movement-history/${batchId}`,
      params: { maxEntries },
    },
    { apiName: this.apiName,...config });
  

  getStockBalance = (batchId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchStockBalanceDto>({
      method: 'GET',
      url: `/api/app/batch/stock-balance/${batchId}`,
    },
    { apiName: this.apiName,...config });
  

  getTraceability = (batchId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchTraceabilityDto>({
      method: 'GET',
      url: `/api/app/batch/traceability/${batchId}`,
    },
    { apiName: this.apiName,...config });
  

  moveBatch = (input: MoveBatchDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MoveBatchResultDto>({
      method: 'POST',
      url: '/api/app/batch/move-batch',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  splitBatch = (input: SplitBatchDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SplitBatchResultDto>({
      method: 'POST',
      url: '/api/app/batch/split-batch',
      body: input,
    },
    { apiName: this.apiName,...config });
}