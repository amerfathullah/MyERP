import type {
  GetSalesPersonWiseTransactionSummaryInput,
  SalesPersonWiseTransactionSummaryReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesPersonWiseTransactionSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: GetSalesPersonWiseTransactionSummaryInput,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, SalesPersonWiseTransactionSummaryReportDto>(
      {
        method: 'GET',
        url: '/api/app/sales-person-wise-transaction-summary/report',
        params: {
          companyId: input.companyId,
          docType: input.docType,
          fromDate: input.fromDate,
          toDate: input.toDate,
          salesPersonId: input.salesPersonId,
          customerId: input.customerId,
          territoryId: input.territoryId,
          itemGroupId: input.itemGroupId,
          itemCode: input.itemCode,
          showReturnEntries: input.showReturnEntries,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
