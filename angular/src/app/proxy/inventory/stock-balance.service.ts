import type { BatchWiseBalanceReportDto, GetBatchWiseBalanceRequestDto, GetItemsAvailabilityInput, GetProductBundleBalanceRequestDto, GetStockBalanceReportRequestDto, GetStockBalanceRequestDto, ItemAvailabilityDto, ProductBundleBalanceReportDto, StockBalanceDto, StockBalanceReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockBalanceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getBatchWiseBalance = (input: GetBatchWiseBalanceRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchWiseBalanceReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/stock-balance/batch-wise-balance',
      params: { companyId: input.companyId, itemId: input.itemId, warehouseId: input.warehouseId, batchId: input.batchId, fromDate: input.fromDate, toDate: input.toDate, includeZeroBalance: input.includeZeroBalance, includeExpiredBatches: input.includeExpiredBatches },
    },
      { apiName: this.apiName,...config });
  

  
  getItemStock = (itemId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockBalanceDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/stock-balance/item-stock/${itemId}`,
    },
      { apiName: this.apiName,...config });
  

  
  getItemsAvailability = (input: GetItemsAvailabilityInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemAvailabilityDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/stock-balance/items-availability',
      params: { itemIds: input.itemIds, warehouseId: input.warehouseId },
    },
      { apiName: this.apiName,...config });
  

  
  getProductBundleBalance = (input: GetProductBundleBalanceRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductBundleBalanceReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/stock-balance/product-bundle-balance',
      params: { companyId: input.companyId, parentItemId: input.parentItemId, warehouseId: input.warehouseId },
    },
      { apiName: this.apiName,...config });
  

  
  getStockBalance = (input: GetStockBalanceRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<StockBalanceDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/stock-balance/stock-balance',
      params: { itemId: input.itemId, warehouseId: input.warehouseId, excludeZeroStock: input.excludeZeroStock, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  getStockBalanceReport = (input: GetStockBalanceReportRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockBalanceReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/stock-balance/stock-balance-report',
      params: { companyId: input.companyId, itemId: input.itemId, warehouseId: input.warehouseId, fromDate: input.fromDate, toDate: input.toDate, showDimensionWiseStock: input.showDimensionWiseStock, excludeZeroStock: input.excludeZeroStock },
    },
      { apiName: this.apiName,...config });
  

  
  recalculateBinQty = (itemId: string, warehouseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/stock-balance/recalculate-bin-qty',
      params: { itemId, warehouseId },
    },
      { apiName: this.apiName,...config });
}
