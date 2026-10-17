import type {
  DeliveryNoteTrendsRequestDto,
  DeliveryNoteTrendsReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DeliveryNoteTrendsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: DeliveryNoteTrendsRequestDto,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, DeliveryNoteTrendsReportDto>(
      {
        method: 'GET',
        url: '/api/app/delivery-note-trends/report',
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
