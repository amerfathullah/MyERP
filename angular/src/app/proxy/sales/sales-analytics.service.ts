import type { SalesAnalyticsReportDto, SalesAnalyticsRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesAnalyticsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getReport = (input: SalesAnalyticsRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesAnalyticsReportDto>({
      method: 'GET',
      url: '/api/app/sales-analytics/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, groupBy: input.groupBy, periodType: input.periodType, valueField: input.valueField },
    },
    { apiName: this.apiName,...config });
}