import type { GeneralLedgerFilterDto, GeneralLedgerReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class GeneralLedgerService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getReport = (input: GeneralLedgerFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GeneralLedgerReportDto>({
      method: 'GET',
      url: '/api/app/general-ledger/report',
      params: { companyId: input.companyId, accountId: input.accountId, fromDate: input.fromDate, toDate: input.toDate },
    },
    { apiName: this.apiName,...config });
}