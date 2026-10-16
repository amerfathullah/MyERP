import type {
  QuotationTrendsRequestDto,
  QuotationTrendsReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class QuotationTrendsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: QuotationTrendsRequestDto,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, QuotationTrendsReportDto>(
      {
        method: 'GET',
        url: '/api/app/quotation-trends/report',
        params: {
          companyId: input.companyId,
          fiscalYearId: input.fiscalYearId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          period: input.period,
          basedOn: input.basedOn,
          groupBy: input.groupBy,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
