import type { GetSalesPartnerTargetVarianceInput, SalesPartnerTargetVarianceReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesPartnerTargetVarianceService {
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
    { apiName: this.apiName,...config });

  constructor(private restService: RestService) {}
}
