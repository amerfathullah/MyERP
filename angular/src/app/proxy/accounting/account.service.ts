import type { AccountDto, AccountTreeNodeDto, CreateUpdateAccountDto, GetAccountListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
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
  

  getList = (input: GetAccountListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AccountDto>>({
      method: 'GET',
      url: '/api/app/account',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTree = (companyId: string, includeDisabled?: boolean, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountTreeNodeDto[]>({
      method: 'GET',
      url: `/api/app/account/tree/${companyId}`,
      params: { includeDisabled },
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