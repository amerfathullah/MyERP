import type { AutoMatchResultDto, EvaluateRulesDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BankTransactionRuleService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<any>>({
      method: 'GET',
      url: '/api/app/bank-transaction-rule',
      params: { ...input },
    },
    { apiName: this.apiName,...config });
  

  create = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any>({
      method: 'POST',
      url: '/api/app/bank-transaction-rule',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  disable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/bank-transaction-rule/${id}/disable`,
    },
    { apiName: this.apiName,...config });
  

  enable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/bank-transaction-rule/${id}/enable`,
    },
    { apiName: this.apiName,...config });
  

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