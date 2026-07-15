import type { CreateOpeningInvoicesDto, CreateOpeningJournalEntryDto, OpeningBalanceResultDto, OpeningInvoiceResultDto, OpeningStatusDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class OpeningBalanceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  createOpeningJournalEntry = (input: CreateOpeningJournalEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpeningBalanceResultDto>({
      method: 'POST',
      url: '/api/app/opening-balance/opening-journal-entry',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createOpeningPurchaseInvoices = (input: CreateOpeningInvoicesDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpeningInvoiceResultDto>({
      method: 'POST',
      url: '/api/app/opening-balance/opening-purchase-invoices',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createOpeningSalesInvoices = (input: CreateOpeningInvoicesDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpeningInvoiceResultDto>({
      method: 'POST',
      url: '/api/app/opening-balance/opening-sales-invoices',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getOpeningStatus = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpeningStatusDto>({
      method: 'GET',
      url: `/api/app/opening-balance/opening-status/${companyId}`,
    },
    { apiName: this.apiName,...config });
}