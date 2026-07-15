import type { AutoMatchResultDto, EvaluateRulesDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BankTransactionRuleService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  evaluateRules = (input: EvaluateRulesDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AutoMatchResultDto>({
      method: 'POST',
      url: '/api/app/bank-transaction-rule/evaluate-rules',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getNextPriority = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'GET',
      url: `/api/app/bank-transaction-rule/next-priority/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  reorderPriorities = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/bank-transaction-rule/reorder-priorities/${companyId}`,
    },
    { apiName: this.apiName,...config });
}