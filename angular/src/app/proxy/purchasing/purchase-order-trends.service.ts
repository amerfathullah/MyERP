import type {
  PurchaseOrderTrendsRequestDto,
  PurchaseOrderTrendsReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PurchaseOrderTrendsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: PurchaseOrderTrendsRequestDto,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, PurchaseOrderTrendsReportDto>(
      {
        method: 'GET',
        url: '/api/app/purchase-order-trends/report',
        params: {
          companyId: input.companyId,
          fiscalYearId: input.fiscalYearId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          period: input.period,
          basedOn: input.basedOn,
          groupBy: input.groupBy,
          supplierId: input.supplierId,
          itemId: input.itemId,
          itemGroup: input.itemGroup,
          includeClosedOrders: input.includeClosedOrders,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
