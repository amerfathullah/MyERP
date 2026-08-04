import type { PurchaseAnalyticsReportDto, PurchaseAnalyticsRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PurchaseAnalyticsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getReport = (input: PurchaseAnalyticsRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseAnalyticsReportDto>({
      method: 'GET',
      url: '/api/app/purchase-analytics/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, groupBy: input.groupBy, periodType: input.periodType, valueField: input.valueField },
    },
    { apiName: this.apiName,...config });
}