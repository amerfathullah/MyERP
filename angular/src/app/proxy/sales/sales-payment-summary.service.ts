import type { SalesPaymentSummaryFilterDto, SalesPaymentSummaryReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesPaymentSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: SalesPaymentSummaryFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPaymentSummaryReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/sales-payment-summary/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, warehouseId: input.warehouseId, posProfileId: input.posProfileId, customerId: input.customerId },
    },
      { apiName: this.apiName,...config });
}
