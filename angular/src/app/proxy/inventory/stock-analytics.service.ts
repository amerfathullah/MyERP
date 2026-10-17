import type { StockAnalyticsReportDto, StockAnalyticsRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockAnalyticsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: StockAnalyticsRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockAnalyticsReportDto>({
      method: 'GET',
      url: '/api/app/stock-analytics/report',
      params: {
        companyId: input.companyId,
        fromDate: input.fromDate,
        toDate: input.toDate,
        range: input.range,
        valueOrQuantity: input.valueOrQuantity,
        warehouseId: input.warehouseId,
        itemGroupId: input.itemGroupId,
        searchTerm: input.searchTerm,
      },
    },
    { apiName: this.apiName,...config });
}
