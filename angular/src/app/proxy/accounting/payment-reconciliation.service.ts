import type { OutstandingInvoiceDto, ReconcilePaymentDto, UnreconcileDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PaymentReconciliationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getOutstandingInvoices = (partyType: string, partyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OutstandingInvoiceDto[]>({
      method: 'GET',
      url: `/api/app/payment-reconciliation/outstanding-invoices/${partyId}`,
      params: { partyType },
    },
    { apiName: this.apiName,...config });
  

  reconcile = (input: ReconcilePaymentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/payment-reconciliation/reconcile',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  unreconcile = (input: UnreconcileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/payment-reconciliation/unreconcile',
      body: input,
    },
    { apiName: this.apiName,...config });
}