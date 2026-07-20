import type { CreateStockClosingDto, StockClosingEntryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class StockClosingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockClosingEntryDto>({
      method: 'POST',
      url: `/api/app/stock-closing/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  generate = (input: CreateStockClosingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockClosingEntryDto>({
      method: 'POST',
      url: '/api/app/stock-closing/generate',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockClosingEntryDto>({
      method: 'GET',
      url: `/api/app/stock-closing/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<StockClosingEntryDto>>({
      method: 'GET',
      url: '/api/app/stock-closing',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockClosingEntryDto>({
      method: 'POST',
      url: `/api/app/stock-closing/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}