import type { CreatePaymentRequestDto, PaymentRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

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
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PaymentRequestDto>>({
      method: 'GET',
      url: '/api/app/payment-request',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentRequestDto>({
      method: 'POST',
      url: `/api/app/payment-request/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}