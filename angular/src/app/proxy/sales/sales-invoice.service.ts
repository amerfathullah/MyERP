import type { CreateInvoiceFromDeliveryNotesDto, CreateSalesInvoiceDto, InvoicePaymentHistoryDto, PaymentScheduleDto, SalesInvoiceDto, SalesInvoiceListSummaryDto, UnbilledDeliveryItemDto, UnbilledOrderItemDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { InvoicePaymentDto } from '../purchasing/models';
import type { BulkOperationResultDto, CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class SalesInvoiceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  amend = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      url: `/api/app/sales-invoice/${id}/amend`,
    },
    { apiName: this.apiName,...config });
  

  bulkPost = (ids: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, BulkOperationResultDto>({
      method: 'POST',
      url: '/api/app/sales-invoice/bulk-post',
      body: ids,
    },
    { apiName: this.apiName,...config });
  

  bulkSubmit = (ids: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, BulkOperationResultDto>({
      method: 'POST',
      url: '/api/app/sales-invoice/bulk-submit',
      body: ids,
    },
    { apiName: this.apiName,...config });
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      url: `/api/app/sales-invoice/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateSalesInvoiceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      url: '/api/app/sales-invoice',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createFromDeliveryNotes = (input: CreateInvoiceFromDeliveryNotesDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      url: '/api/app/sales-invoice/from-delivery-notes',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/sales-invoice/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'GET',
      url: `/api/app/sales-invoice/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SalesInvoiceDto>>({
      method: 'GET',
      url: '/api/app/sales-invoice',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getListSummary = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceListSummaryDto>({
      method: 'GET',
      url: `/api/app/sales-invoice/summary/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getPaymentHistory = (invoiceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoicePaymentHistoryDto[]>({
      method: 'GET',
      url: `/api/app/sales-invoice/payment-history/${invoiceId}`,
    },
    { apiName: this.apiName,...config });
  

  getPaymentSchedule = (invoiceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentScheduleDto[]>({
      method: 'GET',
      url: `/api/app/sales-invoice/payment-schedule/${invoiceId}`,
    },
    { apiName: this.apiName,...config });
  

  getPayments = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoicePaymentDto[]>({
      method: 'GET',
      url: `/api/app/sales-invoice/${id}/payments`,
    },
    { apiName: this.apiName,...config });
  

  getUnbilledDeliveryItems = (customerId: string, companyId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UnbilledDeliveryItemDto[]>({
      method: 'GET',
      url: '/api/app/sales-invoice/unbilled-delivery-items',
      params: { customerId, companyId },
    },
    { apiName: this.apiName,...config });
  

  getUnbilledOrderItems = (customerId: string, companyId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UnbilledOrderItemDto[]>({
      method: 'GET',
      url: '/api/app/sales-invoice/unbilled-order-items',
      params: { customerId, companyId },
    },
    { apiName: this.apiName,...config });
  

  post = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      url: `/api/app/sales-invoice/${id}`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      url: `/api/app/sales-invoice/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  writeOff = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      url: `/api/app/sales-invoice/${id}/write-off`,
    },
    { apiName: this.apiName,...config });
}