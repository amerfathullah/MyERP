import type { BankPaymentFileResultDto, CandidatePaymentEntryDto, CandidatePaymentRequestDto, CreatePaymentOrderDto, GenerateBankFileInput, MakePaymentRecordsDto, PaymentOrderDto, PaymentOrderSummaryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class PaymentOrderService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentOrderDto>({
      method: 'POST',
      url: `/api/app/payment-order/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreatePaymentOrderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentOrderDto>({
      method: 'POST',
      url: '/api/app/payment-order',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/payment-order/${id}`,
    },
    { apiName: this.apiName,...config });
  

  generateBankFile = (id: string, input?: GenerateBankFileInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankPaymentFileResultDto>({
      method: 'POST',
      url: `/api/app/payment-order/${id}/generate-bank-file`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentOrderDto>({
      method: 'GET',
      url: `/api/app/payment-order/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getCandidatePaymentEntries = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CandidatePaymentEntryDto[]>({
      method: 'GET',
      url: `/api/app/payment-order/candidate-payment-entries/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getCandidatePaymentRequests = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CandidatePaymentRequestDto[]>({
      method: 'GET',
      url: `/api/app/payment-order/candidate-payment-requests/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PaymentOrderDto>>({
      method: 'GET',
      url: '/api/app/payment-order',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getSummary = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentOrderSummaryDto>({
      method: 'GET',
      url: `/api/app/payment-order/${id}/summary`,
    },
    { apiName: this.apiName,...config });
  

  makePaymentRecords = (id: string, input: MakePaymentRecordsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'POST',
      responseType: 'text',
      url: `/api/app/payment-order/${id}/make-payment-records`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentOrderDto>({
      method: 'POST',
      url: `/api/app/payment-order/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}