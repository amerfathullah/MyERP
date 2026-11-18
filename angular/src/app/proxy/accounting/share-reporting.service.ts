import type {
  ShareBalanceReportDto,
  ShareBalanceRequestDto,
  ShareLedgerReportDto,
  ShareLedgerRequestDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ShareReportingService {
  private restService = inject(RestService);
  apiName = 'Default';

  getShareBalance = (input: ShareBalanceRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShareBalanceReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/share-reporting/share-balance',
        params: {
          date: input.date,
          shareholderId: input.shareholderId,
          companyId: input.companyId,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getShareLedger = (input: ShareLedgerRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShareLedgerReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/share-reporting/share-ledger',
        params: {
          date: input.date,
          shareholderId: input.shareholderId,
          companyId: input.companyId,
        },
      },
      { apiName: this.apiName, ...config },
    );
}
