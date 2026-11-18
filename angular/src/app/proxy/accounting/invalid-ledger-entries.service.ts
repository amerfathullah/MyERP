import type {
  InvalidLedgerEntriesReportDto,
  InvalidLedgerEntriesRequestDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class InvalidLedgerEntriesService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: InvalidLedgerEntriesRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvalidLedgerEntriesReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/invalid-ledger-entries/report',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          accountIds: input.accountIds,
          voucherNo: input.voucherNo,
        },
      },
      { apiName: this.apiName, ...config },
    );
}
