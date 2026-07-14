import type { CreatePaymentRequestDto, PaymentRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PaymentRequestService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentRequestDto>({
      method: 'POST',
      url: `/api/app/payment-request/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreatePaymentRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentRequestDto>({
      method: 'POST',
      url: '/api/app/payment-request',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PaymentRequestDto>>({
      method: 'GET',
      url: '/api/app/payment-request',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentRequestDto>({
      method: 'POST',
      url: `/api/app/payment-request/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}