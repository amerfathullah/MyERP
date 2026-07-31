import type { BankAccountDto, CreateUpdateBankAccountDto, GetBankAccountListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BankAccountService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateBankAccountDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankAccountDto>({
      method: 'POST',
      url: '/api/app/bank-account',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/bank-account/${id}`,
    },
    { apiName: this.apiName,...config });
  

  disable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankAccountDto>({
      method: 'POST',
      url: `/api/app/bank-account/${id}/disable`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankAccountDto>({
      method: 'GET',
      url: `/api/app/bank-account/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetBankAccountListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BankAccountDto>>({
      method: 'GET',
      url: '/api/app/bank-account',
      params: { companyId: input.companyId, filter: input.filter, isCompanyAccount: input.isCompanyAccount, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  setAsDefault = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankAccountDto>({
      method: 'POST',
      url: `/api/app/bank-account/${id}/set-as-default`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateBankAccountDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankAccountDto>({
      method: 'PUT',
      url: `/api/app/bank-account/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}