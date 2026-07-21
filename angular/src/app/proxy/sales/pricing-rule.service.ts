import type { ApplyPricingRuleDto, CreatePricingRuleDto, PricingRuleDto, PricingRuleResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PricingRuleService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  apply = (input: ApplyPricingRuleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PricingRuleResultDto[]>({
      method: 'POST',
      url: '/api/app/pricing-rule/apply',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreatePricingRuleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PricingRuleDto>({
      method: 'POST',
      url: '/api/app/pricing-rule',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/pricing-rule/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PricingRuleDto>({
      method: 'GET',
      url: `/api/app/pricing-rule/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PricingRuleDto>>({
      method: 'GET',
      url: '/api/app/pricing-rule',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}