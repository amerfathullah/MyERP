import type { GetSalesPartnerTargetVarianceInput, SalesPartnerTargetVarianceReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesPartnerTargetVarianceService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: GetSalesPartnerTargetVarianceInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPartnerTargetVarianceReportDto>({
      method: 'GET',
      url: '/api/app/sales-partner-target-variance/report',
      params: {
        companyId: input.companyId,
        docType: input.docType,
        targetOn: input.targetOn,
        fiscalYearId: input.fiscalYearId,
        fromDate: input.fromDate,
        toDate: input.toDate,
        salesPartnerId: input.salesPartnerId,
        itemGroupId: input.itemGroupId,
      },
    },
    { apiName: this.apiName, ...config });
}
