import type { AutoMatchResult } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BankAutoMatchService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  autoMatch = (bankAccountId: string, companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AutoMatchResult>({
      method: 'POST',
      url: '/api/app/bank-auto-match/auto-match',
      params: { bankAccountId, companyId },
    },
    { apiName: this.apiName,...config });
}