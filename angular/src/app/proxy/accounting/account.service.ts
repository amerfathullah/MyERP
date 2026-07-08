import type { AccountDto, CreateUpdateAccountDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AccountService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateAccountDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountDto>({
      method: 'POST',
      url: '/api/app/account',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/account/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountDto>({
      method: 'GET',
      url: `/api/app/account/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AccountDto>>({
      method: 'GET',
      url: '/api/app/account',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateAccountDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountDto>({
      method: 'PUT',
      url: `/api/app/account/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}