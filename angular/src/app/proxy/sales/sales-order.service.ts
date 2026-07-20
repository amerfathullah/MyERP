import type { CreateSalesOrderDto, SalesOrderDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { BulkOperationResultDto, CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class SalesOrderService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  bulkSubmit = (ids: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, BulkOperationResultDto>({
      method: 'POST',
      url: '/api/app/sales-order/bulk-submit',
      body: ids,
    },
    { apiName: this.apiName,...config });
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesOrderDto>({
      method: 'POST',
      url: `/api/app/sales-order/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  close = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesOrderDto>({
      method: 'POST',
      url: `/api/app/sales-order/${id}/close`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateSalesOrderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesOrderDto>({
      method: 'POST',
      url: '/api/app/sales-order',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/sales-order/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesOrderDto>({
      method: 'GET',
      url: `/api/app/sales-order/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SalesOrderDto>>({
      method: 'GET',
      url: '/api/app/sales-order',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  reopen = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesOrderDto>({
      method: 'POST',
      url: `/api/app/sales-order/${id}/reopen`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesOrderDto>({
      method: 'POST',
      url: `/api/app/sales-order/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateSalesOrderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesOrderDto>({
      method: 'PUT',
      url: `/api/app/sales-order/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}