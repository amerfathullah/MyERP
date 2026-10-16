import type {
  GetSalesOrderAnalysisDto,
  SalesOrderAnalysisReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesOrderAnalysisService {
  private restService = inject(RestService);
  apiName = 'Default';

  getAnalysis = (input: GetSalesOrderAnalysisDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesOrderAnalysisReportDto>(
      {
        method: 'GET',
        url: '/api/app/sales-order-analysis/analysis',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          customerId: input.customerId,
          salesOrderId: input.salesOrderId,
          itemId: input.itemId,
          groupBySo: input.groupBySo,
          groupByItem: input.groupByItem,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
