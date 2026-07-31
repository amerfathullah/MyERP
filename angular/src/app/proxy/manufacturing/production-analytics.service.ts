import type { ProductionAnalyticsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ProductionAnalyticsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getAnalytics = (companyId: string, fromDate: string, toDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionAnalyticsDto>({
      method: 'GET',
      url: `/api/app/production-analytics/analytics/${companyId}`,
      params: { fromDate, toDate },
    },
    { apiName: this.apiName,...config });
}