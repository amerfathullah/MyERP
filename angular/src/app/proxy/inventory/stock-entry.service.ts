import type { CreateStockEntryDto, StockEntryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockEntryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'POST',
      url: `/api/app/stock-entry/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateStockEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'POST',
      url: '/api/app/stock-entry',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'GET',
      url: `/api/app/stock-entry/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<StockEntryDto>>({
      method: 'GET',
      url: '/api/app/stock-entry',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  post = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'POST',
      url: `/api/app/stock-entry/${id}`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'POST',
      url: `/api/app/stock-entry/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}