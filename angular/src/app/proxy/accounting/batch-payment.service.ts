import type { BatchPaymentInvoiceDto, BatchPaymentResultDto, CreateBatchPaymentDto, GetOutstandingForBatchDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BatchPaymentService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  createBatchPayment = (input: CreateBatchPaymentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchPaymentResultDto>({
      method: 'POST',
      url: '/api/app/batch-payment/batch-payment',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getOutstandingInvoices = (input: GetOutstandingForBatchDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchPaymentInvoiceDto[]>({
      method: 'GET',
      url: '/api/app/batch-payment/outstanding-invoices',
      params: { companyId: input.companyId, partyType: input.partyType, partyId: input.partyId },
    },
    { apiName: this.apiName,...config });
}