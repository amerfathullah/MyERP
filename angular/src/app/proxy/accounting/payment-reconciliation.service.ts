import type { LinkedAllocationDto, OutstandingInvoiceDto, ReconcileAllocationDto, ReconcilePaymentDto, UnreconcileDto, UnreconciledPaymentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PaymentReconciliationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getAutoAllocation = (partyType: string, partyId: string, accountId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReconcileAllocationDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/payment-reconciliation/auto-allocation',
      params: { partyType, partyId, accountId },
    },
      { apiName: this.apiName,...config });
  

  
  getLinkedAllocations = (voucherType: string, voucherId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LinkedAllocationDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/payment-reconciliation/linked-allocations/${voucherId}`,
      params: { voucherType },
    },
      { apiName: this.apiName,...config });
  

  
  getOutstandingInvoices = (partyType: string, partyId: string, accountId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OutstandingInvoiceDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/payment-reconciliation/outstanding-invoices',
      params: { partyType, partyId, accountId },
    },
      { apiName: this.apiName,...config });
  

  
  getUnreconciledPayments = (partyType: string, partyId: string, accountId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UnreconciledPaymentDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/payment-reconciliation/unreconciled-payments',
      params: { partyType, partyId, accountId },
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
  

  
  unreconcileAllocations = (inputs: readonly UnreconcileDto[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/payment-reconciliation/unreconcile-allocations',
      body: inputs,
    },
      { apiName: this.apiName,...config });
}
