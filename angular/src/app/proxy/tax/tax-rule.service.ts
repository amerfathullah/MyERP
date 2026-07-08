import type { CreateUpdateTaxRuleDto, TaxRuleDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TaxRuleService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateTaxRuleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxRuleDto>({
      method: 'POST',
      url: '/api/app/tax-rule',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/tax-rule/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (taxCategoryId: string, input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TaxRuleDto>>({
      method: 'GET',
      url: '/api/app/tax-rule',
      params: { taxCategoryId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateTaxRuleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxRuleDto>({
      method: 'PUT',
      url: `/api/app/tax-rule/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}