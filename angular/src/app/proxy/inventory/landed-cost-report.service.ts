import type { LandedCostReportFilterDto, LandedCostReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LandedCostReportService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: LandedCostReportFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LandedCostReportDto>(
      {
        method: 'GET',
        url: '/api/app/landed-cost-report/report',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          receiptDocumentType: input.receiptDocumentType,
          receiptDocumentNo: input.receiptDocumentNo,
          status: input.status,
          searchTerm: input.searchTerm,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
