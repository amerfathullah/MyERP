import type { CreatePosClosingDto, PosClosingDto, PosClosingInvoiceDto, PosExpectedPaymentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class PosClosingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  calculateExpectedAmounts = (posOpeningEntryId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosExpectedPaymentDto[]>({
      method: 'POST',
      url: `/api/app/pos-closing/calculate-expected-amounts/${posOpeningEntryId}`,
    },
    { apiName: this.apiName,...config });
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosClosingDto>({
      method: 'POST',
      url: `/api/app/pos-closing/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreatePosClosingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosClosingDto>({
      method: 'POST',
      url: '/api/app/pos-closing',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosClosingDto>({
      method: 'GET',
      url: `/api/app/pos-closing/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getUnconsolidatedInvoices = (posOpeningEntryId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosClosingInvoiceDto[]>({
      method: 'GET',
      url: `/api/app/pos-closing/unconsolidated-invoices/${posOpeningEntryId}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PosClosingDto>>({
      method: 'GET',
      url: '/api/app/pos-closing',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  retry = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosClosingDto>({
      method: 'POST',
      url: `/api/app/pos-closing/${id}/retry`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosClosingDto>({
      method: 'POST',
      url: `/api/app/pos-closing/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}