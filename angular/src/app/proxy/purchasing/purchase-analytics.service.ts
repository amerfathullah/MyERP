import type { PurchaseAnalyticsReportDto, PurchaseAnalyticsRequestDto } from './models';
import { Injectable } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';

@Injectable({ providedIn: 'root' })
export class PurchaseAnalyticsService {
  private apiName = 'Default';

  constructor(private restService: RestService) {}

  getReport = (input: PurchaseAnalyticsRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseAnalyticsReportDto>({
      method: 'GET',
      url: '/api/app/purchase-analytics/report',
      params: { ...input },
    }, { apiName: this.apiName, ...config });
}
