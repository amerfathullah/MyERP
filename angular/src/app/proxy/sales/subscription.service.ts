import type { CreateSubscriptionDto, GeneratedInvoiceDto, SubscriptionDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SubscriptionService {
  private restService = inject(RestService);
  apiName = 'Default';


  advancePeriod = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubscriptionDto>({
      method: 'POST',
      url: `/api/app/subscription/${id}/advance-period`,
    },
    { apiName: this.apiName,...config });


  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubscriptionDto>({
      method: 'POST',
      url: `/api/app/subscription/${id}/cancel`,
    },
    { apiName: this.apiName,...config });


  create = (input: CreateSubscriptionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubscriptionDto>({
      method: 'POST',
      url: '/api/app/subscription',
      body: input,
    },
    { apiName: this.apiName,...config });


  generateInvoice = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GeneratedInvoiceDto>({
      method: 'POST',
      url: `/api/app/subscription/${id}/generate-invoice`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubscriptionDto>({
      method: 'GET',
      url: `/api/app/subscription/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SubscriptionDto>>({
      method: 'GET',
      url: '/api/app/subscription',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount, filter: input.filter },
    },
    { apiName: this.apiName,...config });
}
