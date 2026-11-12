import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CreateStockReconciliationDto, GetStockBalanceForInputDto, GetStockReconciliationItemsInputDto, GetStockReconciliationListDto, StockBalanceForPreviewDto, StockReconciliationDto, StockReconciliationItemPreviewDto } from '../dtos/models';

@Injectable({
  providedIn: 'root',
})
export class StockReconciliationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockReconciliationDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/stock-reconciliation/${id}/cancel`,
    },
      { apiName: this.apiName,...config });
  

  
  create = (input: CreateStockReconciliationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockReconciliationDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/stock-reconciliation',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockReconciliationDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/stock-reconciliation/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getItemsForReconciliation = (input: GetStockReconciliationItemsInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockReconciliationItemPreviewDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/stock-reconciliation/items-for-reconciliation',
      params: { companyId: input.companyId, warehouseId: input.warehouseId, postingDate: input.postingDate, itemId: input.itemId, ignoreEmptyStock: input.ignoreEmptyStock },
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: GetStockReconciliationListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<StockReconciliationDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/stock-reconciliation',
      params: { companyId: input.companyId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  getStockBalanceFor = (input: GetStockBalanceForInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockBalanceForPreviewDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/stock-reconciliation/stock-balance-for',
      params: { companyId: input.companyId, itemId: input.itemId, warehouseId: input.warehouseId, postingDate: input.postingDate, batchId: input.batchId },
    },
      { apiName: this.apiName,...config });
  

  
  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockReconciliationDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/stock-reconciliation/${id}/submit`,
    },
      { apiName: this.apiName,...config });
}
