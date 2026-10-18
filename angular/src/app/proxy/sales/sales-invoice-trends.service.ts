import type {
  SalesInvoiceTrendsRequestDto,
  SalesInvoiceTrendsReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesInvoiceTrendsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: SalesInvoiceTrendsRequestDto,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, SalesInvoiceTrendsReportDto>(
      {
        method: 'GET',
        url: '/api/app/sales-invoice-trends/report',
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
