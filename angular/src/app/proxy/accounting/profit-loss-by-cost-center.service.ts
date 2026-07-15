import type { ProfitLossByCostCenterDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ProfitLossByCostCenterService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getReport = (companyId: string, fromDate: string, toDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProfitLossByCostCenterDto>({
      method: 'GET',
      url: `/api/app/profit-loss-by-cost-center/report/${companyId}`,
      params: { fromDate, toDate },
    },
    { apiName: this.apiName,...config });
}