import type {
  CustomerLedgerSummaryRequestDto,
  CustomerLedgerSummaryReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CustomerLedgerSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: CustomerLedgerSummaryRequestDto,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, CustomerLedgerSummaryReportDto>(
      {
        method: 'GET',
        url: '/api/app/customer-ledger-summary/report',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          customerId: input.customerId,
          customerGroupId: input.customerGroupId,
          territoryId: input.territoryId,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
