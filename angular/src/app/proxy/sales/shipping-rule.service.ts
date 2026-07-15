import type { CreateShippingRuleDto, ShippingRuleDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ShippingRuleService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  calculate = (ruleId: string, value: number, countryCode?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'POST',
      url: `/api/app/shipping-rule/calculate/${ruleId}`,
      params: { value, countryCode },
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateShippingRuleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShippingRuleDto>({
      method: 'POST',
      url: '/api/app/shipping-rule',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/shipping-rule/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShippingRuleDto>({
      method: 'GET',
      url: `/api/app/shipping-rule/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ShippingRuleDto>>({
      method: 'GET',
      url: '/api/app/shipping-rule',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  toggle = (id: string, isEnabled: boolean, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShippingRuleDto>({
      method: 'POST',
      url: `/api/app/shipping-rule/${id}/toggle`,
      params: { isEnabled },
    },
    { apiName: this.apiName,...config });
}