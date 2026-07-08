import type { CreateSalesInvoiceDto, SalesInvoiceDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesInvoiceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      url: `/api/app/sales-invoice/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateSalesInvoiceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      url: '/api/app/sales-invoice',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'GET',
      url: `/api/app/sales-invoice/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SalesInvoiceDto>>({
      method: 'GET',
      url: '/api/app/sales-invoice',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  post = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      url: `/api/app/sales-invoice/${id}`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      url: `/api/app/sales-invoice/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}