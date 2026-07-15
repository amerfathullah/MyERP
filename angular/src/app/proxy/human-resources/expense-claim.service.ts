import type { CreateExpenseClaimDto, ExpenseClaimDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ExpenseClaimService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  approve = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExpenseClaimDto>({
      method: 'POST',
      url: `/api/app/expense-claim/${id}/approve`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateExpenseClaimDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExpenseClaimDto>({
      method: 'POST',
      url: '/api/app/expense-claim',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExpenseClaimDto>({
      method: 'GET',
      url: `/api/app/expense-claim/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ExpenseClaimDto>>({
      method: 'GET',
      url: '/api/app/expense-claim',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  reimburse = (id: string, paidFromAccountId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'POST',
      responseType: 'text',
      url: `/api/app/expense-claim/${id}/reimburse/${paidFromAccountId}`,
    },
    { apiName: this.apiName,...config });
  

  reject = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExpenseClaimDto>({
      method: 'POST',
      url: `/api/app/expense-claim/${id}/reject`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExpenseClaimDto>({
      method: 'POST',
      url: `/api/app/expense-claim/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}