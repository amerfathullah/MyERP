import type { AgingReportDto, AgingReportRequestDto, SendPaymentReminderInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AgingReportService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getPayablesAging = (input: AgingReportRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AgingReportDto>({
      method: 'GET',
      url: '/api/app/aging-report/payables-aging',
      params: { companyId: input.companyId, asOfDate: input.asOfDate },
    },
    { apiName: this.apiName,...config });
  

  getReceivablesAging = (input: AgingReportRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AgingReportDto>({
      method: 'GET',
      url: '/api/app/aging-report/receivables-aging',
      params: { companyId: input.companyId, asOfDate: input.asOfDate },
    },
    { apiName: this.apiName,...config });
  

  sendPaymentReminder = (input: SendPaymentReminderInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'POST',
      url: '/api/app/aging-report/send-payment-reminder',
      body: input,
    },
    { apiName: this.apiName,...config });
}