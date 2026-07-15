import type { CreateRevaluationDto, EligibleAccountDto, ExchangeRateRevaluationDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ExchangeRateRevaluationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  createRevaluation = (input: CreateRevaluationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExchangeRateRevaluationDto>({
      method: 'POST',
      url: '/api/app/exchange-rate-revaluation/revaluation',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getEligibleAccounts = (companyId: string, companyCurrency: string, postingDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EligibleAccountDto[]>({
      method: 'GET',
      url: `/api/app/exchange-rate-revaluation/eligible-accounts/${companyId}`,
      params: { companyCurrency, postingDate },
    },
    { apiName: this.apiName,...config });
}