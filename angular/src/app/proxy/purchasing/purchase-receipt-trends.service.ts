import type {
  PurchaseReceiptTrendsRequestDto,
  PurchaseReceiptTrendsReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PurchaseReceiptTrendsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: PurchaseReceiptTrendsRequestDto,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, PurchaseReceiptTrendsReportDto>(
      {
        method: 'GET',
        url: '/api/app/purchase-receipt-trends/report',
        params: {
          companyId: input.companyId,
          supplierId: input.supplierId,
          fiscalYearId: input.fiscalYearId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          period: input.period,
          basedOn: input.basedOn,
          groupBy: input.groupBy,
          includeReturns: input.includeReturns,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
