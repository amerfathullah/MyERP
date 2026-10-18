import type {
  PurchaseInvoiceTrendsRequestDto,
  PurchaseInvoiceTrendsReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PurchaseInvoiceTrendsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: PurchaseInvoiceTrendsRequestDto,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, PurchaseInvoiceTrendsReportDto>(
      {
        method: 'GET',
        url: '/api/app/purchase-invoice-trends/report',
        params: {
          companyId: input.companyId,
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
