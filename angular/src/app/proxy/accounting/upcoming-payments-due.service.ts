import type { GetUpcomingPaymentsDueInput, UpcomingPaymentsDueReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class UpcomingPaymentsDueService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getReport = (input: GetUpcomingPaymentsDueInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UpcomingPaymentsDueReportDto>({
      method: 'GET',
      url: '/api/app/upcoming-payments-due/report',
      params: { companyId: input.companyId, daysAhead: input.daysAhead, supplierId: input.supplierId },
    },
    { apiName: this.apiName,...config });
}