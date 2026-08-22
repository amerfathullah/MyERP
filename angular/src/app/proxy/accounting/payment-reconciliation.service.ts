import type { OutstandingInvoiceDto, ReconcileAllocationDto, ReconcilePaymentDto, UnreconcileDto, UnreconciledPaymentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PaymentReconciliationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getAutoAllocation = (partyType: string, partyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReconcileAllocationDto[]>({
      method: 'GET',
      url: `/api/app/payment-reconciliation/auto-allocation/${partyId}`,
      params: { partyType },
    },
    { apiName: this.apiName,...config });
  

  getOutstandingInvoices = (partyType: string, partyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OutstandingInvoiceDto[]>({
      method: 'GET',
      url: `/api/app/payment-reconciliation/outstanding-invoices/${partyId}`,
      params: { partyType },
    },
    { apiName: this.apiName,...config });
  

  getUnreconciledPayments = (partyType: string, partyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UnreconciledPaymentDto[]>({
      method: 'GET',
      url: `/api/app/payment-reconciliation/unreconciled-payments/${partyId}`,
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