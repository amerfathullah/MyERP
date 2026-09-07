import type { AutoAllocateRequestDto, AutoAllocationResultDto, CreatePaymentEntryDto, OutstandingInvoiceForPaymentDto, PartyOutstandingDto, PaymentEntryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { BulkOperationResultDto, CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class PaymentEntryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  amend = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentEntryDto>({
      method: 'POST',
      url: `/api/app/payment-entry/${id}/amend`,
    },
    { apiName: this.apiName,...config });
  

  autoAllocate = (input: AutoAllocateRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AutoAllocationResultDto>({
      method: 'POST',
      url: '/api/app/payment-entry/auto-allocate',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  bulkPost = (ids: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, BulkOperationResultDto>({
      method: 'POST',
      url: '/api/app/payment-entry/bulk-post',
      body: ids,
    },
    { apiName: this.apiName,...config });
  

  bulkSubmit = (ids: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, BulkOperationResultDto>({
      method: 'POST',
      url: '/api/app/payment-entry/bulk-submit',
      body: ids,
    },
    { apiName: this.apiName,...config });
  

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
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/payment-entry/${id}`,
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
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getOutstandingForParty = (partyType: string, partyId: string, companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OutstandingInvoiceForPaymentDto[]>({
      method: 'GET',
      url: '/api/app/payment-entry/outstanding-for-party',
      params: { partyType, partyId, companyId },
    },
    { apiName: this.apiName,...config });
  

  getPartyOutstanding = (partyType: string, partyId: string, companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyOutstandingDto>({
      method: 'GET',
      url: '/api/app/payment-entry/party-outstanding',
      params: { partyType, partyId, companyId },
    },
    { apiName: this.apiName,...config });
  

  getPaymentEntryTemplate = (documentType: string, documentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CreatePaymentEntryDto>({
      method: 'GET',
      url: `/api/app/payment-entry/payment-entry-template/${documentId}`,
      params: { documentType },
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
}