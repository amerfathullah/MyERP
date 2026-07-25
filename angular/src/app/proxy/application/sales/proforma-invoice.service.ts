import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CreateProformaInvoiceDto, ProformaInvoiceDto, ProformedTotalsDto, SendProformaEmailDto } from '../contracts/sales/models';

@Injectable({
  providedIn: 'root',
})
export class ProformaInvoiceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/proforma-invoice/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateProformaInvoiceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProformaInvoiceDto>({
      method: 'POST',
      url: '/api/app/proforma-invoice',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProformaInvoiceDto>({
      method: 'GET',
      url: `/api/app/proforma-invoice/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getForSalesOrder = (salesOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProformaInvoiceDto[]>({
      method: 'GET',
      url: `/api/app/proforma-invoice/for-sales-order/${salesOrderId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ProformaInvoiceDto>>({
      method: 'GET',
      url: '/api/app/proforma-invoice',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getProformedTotals = (salesOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProformedTotalsDto[]>({
      method: 'GET',
      url: `/api/app/proforma-invoice/proformed-totals/${salesOrderId}`,
    },
    { apiName: this.apiName,...config });
  

  sendEmail = (id: string, input: SendProformaEmailDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/proforma-invoice/${id}/send-email`,
      body: input,
    },
    { apiName: this.apiName,...config });
}