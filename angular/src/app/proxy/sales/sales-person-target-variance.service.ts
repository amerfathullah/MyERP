import type {
  GetSalesPersonTargetVarianceInput,
  SalesPersonTargetVarianceReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesPersonTargetVarianceService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: GetSalesPersonTargetVarianceInput,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, SalesPersonTargetVarianceReportDto>(
      {
        method: 'GET',
        url: '/api/app/sales-person-target-variance/report',
        params: {
          companyId: input.companyId,
          docType: input.docType,
          targetOn: input.targetOn,
          fiscalYearId: input.fiscalYearId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          salesPersonId: input.salesPersonId,
          itemGroupId: input.itemGroupId,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
