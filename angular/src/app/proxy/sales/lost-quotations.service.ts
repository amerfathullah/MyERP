import type {
  GetLostQuotationsReportInput,
  LostQuotationsReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LostQuotationsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: GetLostQuotationsReportInput,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, LostQuotationsReportDto>(
      {
        method: 'GET',
        url: '/api/app/lost-quotations/report',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          timespan: input.timespan,
          groupBy: input.groupBy,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
