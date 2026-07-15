import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CreateStockReconciliationDto, GetStockReconciliationListDto, StockReconciliationDto } from '../dtos/models';

@Injectable({
  providedIn: 'root',
})
export class StockReconciliationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockReconciliationDto>({
      method: 'POST',
      url: `/api/app/stock-reconciliation/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateStockReconciliationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockReconciliationDto>({
      method: 'POST',
      url: '/api/app/stock-reconciliation',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockReconciliationDto>({
      method: 'GET',
      url: `/api/app/stock-reconciliation/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetStockReconciliationListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<StockReconciliationDto>>({
      method: 'GET',
      url: '/api/app/stock-reconciliation',
      params: { companyId: input.companyId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockReconciliationDto>({
      method: 'POST',
      url: `/api/app/stock-reconciliation/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}