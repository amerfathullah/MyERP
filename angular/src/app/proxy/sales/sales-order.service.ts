import type { CreateSalesOrderDto, SalesOrderDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesOrderService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesOrderDto>({
      method: 'POST',
      url: `/api/app/sales-order/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateSalesOrderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesOrderDto>({
      method: 'POST',
      url: '/api/app/sales-order',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesOrderDto>({
      method: 'GET',
      url: `/api/app/sales-order/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SalesOrderDto>>({
      method: 'GET',
      url: '/api/app/sales-order',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesOrderDto>({
      method: 'POST',
      url: `/api/app/sales-order/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}