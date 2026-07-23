import type { FreezeAccountingPeriodDto, MonthEndCloseRequestDto, MonthEndCloseStatusDto, MonthEndReadinessDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MonthEndCloseService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  freeze = (input: FreezeAccountingPeriodDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/month-end-close/freeze',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getCloseStatus = (input: MonthEndCloseRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MonthEndCloseStatusDto>({
      method: 'GET',
      url: '/api/app/month-end-close/close-status',
      params: { companyId: input.companyId, periodEndDate: input.periodEndDate },
    },
    { apiName: this.apiName,...config });
  

  validateReadiness = (input: MonthEndCloseRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MonthEndReadinessDto>({
      method: 'POST',
      url: '/api/app/month-end-close/validate-readiness',
      body: input,
    },
    { apiName: this.apiName,...config });
}