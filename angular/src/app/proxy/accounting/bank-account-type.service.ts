import type { BankAccountTypeDto, CreateUpdateBankAccountTypeDto, GetBankAccountTypeListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BankAccountTypeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateBankAccountTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankAccountTypeDto>({
      method: 'POST',
      url: '/api/app/bank-account-type',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/bank-account-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankAccountTypeDto>({
      method: 'GET',
      url: `/api/app/bank-account-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetBankAccountTypeListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BankAccountTypeDto>>({
      method: 'GET',
      url: '/api/app/bank-account-type',
      params: { filter: input.filter, isActive: input.isActive, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateBankAccountTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankAccountTypeDto>({
      method: 'PUT',
      url: `/api/app/bank-account-type/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}