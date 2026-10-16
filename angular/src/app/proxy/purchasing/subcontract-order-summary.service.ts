import type {
  SubcontractOrderSummaryFilterDto,
  SubcontractOrderSummaryReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SubcontractOrderSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: SubcontractOrderSummaryFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractOrderSummaryReportDto>(
      {
        method: 'GET',
        url: '/api/app/subcontract-order-summary/report',
        params: {
          companyId: input.companyId,
          supplierId: input.supplierId,
          subcontractOrderId: input.subcontractOrderId,
          fromDate: input.fromDate,
          toDate: input.toDate,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
