import type { CreatePaymentEntryDto, OutstandingInvoiceForPaymentDto, PaymentEntryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class PaymentEntryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentEntryDto>({
      method: 'POST',
      url: `/api/app/payment-entry/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

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
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PaymentEntryDto>>({
      method: 'GET',
      url: '/api/app/payment-entry',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getOutstandingForParty = (partyType: string, partyId: string, companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OutstandingInvoiceForPaymentDto[]>({
      method: 'GET',
      url: '/api/app/payment-entry/outstanding-for-party',
      params: { partyType, partyId, companyId },
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

  update = (id: string, input: CreatePaymentEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentEntryDto>({
      method: 'PUT',
      url: `/api/app/payment-entry/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/payment-entry/${id}`,
    },
    { apiName: this.apiName,...config });
}