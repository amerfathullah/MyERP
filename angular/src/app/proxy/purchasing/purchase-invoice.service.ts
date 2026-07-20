import type { CreatePurchaseInvoiceDto, PurchaseInvoiceDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { PaymentScheduleDto } from '../sales/models';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class PurchaseInvoiceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  amend = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseInvoiceDto>({
      method: 'POST',
      url: `/api/app/purchase-invoice/${id}/amend`,
    },
    { apiName: this.apiName,...config });
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseInvoiceDto>({
      method: 'POST',
      url: `/api/app/purchase-invoice/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreatePurchaseInvoiceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseInvoiceDto>({
      method: 'POST',
      url: '/api/app/purchase-invoice',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseInvoiceDto>({
      method: 'GET',
      url: `/api/app/purchase-invoice/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PurchaseInvoiceDto>>({
      method: 'GET',
      url: '/api/app/purchase-invoice',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getPaymentSchedule = (invoiceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentScheduleDto[]>({
      method: 'GET',
      url: `/api/app/purchase-invoice/payment-schedule/${invoiceId}`,
    },
    { apiName: this.apiName,...config });
  

  post = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseInvoiceDto>({
      method: 'POST',
      url: `/api/app/purchase-invoice/${id}`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseInvoiceDto>({
      method: 'POST',
      url: `/api/app/purchase-invoice/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  writeOff = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseInvoiceDto>({
      method: 'POST',
      url: `/api/app/purchase-invoice/${id}/write-off`,
    },
    { apiName: this.apiName,...config });

  update = (id: string, input: CreatePurchaseInvoiceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseInvoiceDto>({
      method: 'PUT',
      url: `/api/app/purchase-invoice/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/purchase-invoice/${id}`,
    },
    { apiName: this.apiName,...config });
}