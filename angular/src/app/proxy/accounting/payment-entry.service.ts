import type { CreatePaymentEntryDto, PaymentEntryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PaymentEntryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreatePaymentEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentEntryDto>({
      method: 'POST',
      url: '/api/app/payment-entry',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentEntryDto>({
      method: 'GET',
      url: `/api/app/payment-entry/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PaymentEntryDto>>({
      method: 'GET',
      url: '/api/app/payment-entry',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  post = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentEntryDto>({
      method: 'POST',
      url: `/api/app/payment-entry/${id}`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentEntryDto>({
      method: 'POST',
      url: `/api/app/payment-entry/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}