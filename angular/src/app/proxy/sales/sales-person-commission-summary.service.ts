import type {
  GetSalesPersonCommissionSummaryInput,
  SalesPersonCommissionSummaryReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesPersonCommissionSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: GetSalesPersonCommissionSummaryInput,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, SalesPersonCommissionSummaryReportDto>(
      {
        method: 'GET',
        url: '/api/app/sales-person-commission-summary/report',
        params: {
          companyId: input.companyId,
          docType: input.docType,
          fromDate: input.fromDate,
          toDate: input.toDate,
          salesPersonId: input.salesPersonId,
          customerId: input.customerId,
          territoryId: input.territoryId,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
