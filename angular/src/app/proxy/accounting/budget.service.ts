import type { BudgetDto, CreateBudgetDto, GetBudgetListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class BudgetService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: GetBudgetListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BudgetDto>>({
      method: 'GET',
      url: '/api/app/budget',
      params: { companyId: input.companyId, fiscalYearId: input.fiscalYearId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BudgetDto>({
      method: 'GET',
      url: `/api/app/budget/${id}`,
    }, { apiName: this.apiName, ...config });

  create = (input: CreateBudgetDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BudgetDto>({
      method: 'POST',
      url: '/api/app/budget',
      body: input,
    }, { apiName: this.apiName, ...config });

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BudgetDto>({
      method: 'POST',
      url: `/api/app/budget/${id}/submit`,
    }, { apiName: this.apiName, ...config });

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BudgetDto>({
      method: 'POST',
      url: `/api/app/budget/${id}/cancel`,
    }, { apiName: this.apiName, ...config });
}
