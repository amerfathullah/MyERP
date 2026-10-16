import type {
  SalesOrderTrendsRequestDto,
  SalesOrderTrendsReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesOrderTrendsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: SalesOrderTrendsRequestDto,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, SalesOrderTrendsReportDto>(
      {
        method: 'GET',
        url: '/api/app/sales-order-trends/report',
        params: {
          companyId: input.companyId,
          fiscalYearId: input.fiscalYearId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          period: input.period,
          basedOn: input.basedOn,
          groupBy: input.groupBy,
          includeClosedOrders: input.includeClosedOrders,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
