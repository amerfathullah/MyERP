import type { AccountCategoryDto, CreateAccountCategoryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AccountCategoryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateAccountCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountCategoryDto>({
      method: 'POST',
      url: '/api/app/account-category',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/account-category/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountCategoryDto>({
      method: 'GET',
      url: `/api/app/account-category/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AccountCategoryDto>>({
      method: 'GET',
      url: '/api/app/account-category',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}