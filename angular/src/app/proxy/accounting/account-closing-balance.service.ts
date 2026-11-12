import type { AccountClosingBalanceDto, ClosingBalanceStatusDto, RebuildClosingBalanceDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AccountClosingBalanceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getList = (companyId: string, period: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountClosingBalanceDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/account-closing-balance',
      params: { companyId, period },
    },
      { apiName: this.apiName,...config });
  

  
  getStatus = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ClosingBalanceStatusDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/account-closing-balance/status/${companyId}`,
    },
      { apiName: this.apiName,...config });
  

  
  rebuild = (input: RebuildClosingBalanceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/account-closing-balance/rebuild',
      body: input,
    },
      { apiName: this.apiName,...config });
}
