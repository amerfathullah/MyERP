import type { BankAccountSubtypeDto, CreateUpdateBankAccountSubtypeDto, GetBankAccountSubtypeListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BankAccountSubtypeService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreateUpdateBankAccountSubtypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankAccountSubtypeDto>({
      method: 'POST',
      url: '/api/app/bank-account-subtype',
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/bank-account-subtype/${id}`,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankAccountSubtypeDto>({
      method: 'GET',
      url: `/api/app/bank-account-subtype/${id}`,
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetBankAccountSubtypeListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BankAccountSubtypeDto>>({
      method: 'GET',
      url: '/api/app/bank-account-subtype',
      params: { filter: input.filter, isActive: input.isActive, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateBankAccountSubtypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankAccountSubtypeDto>({
      method: 'PUT',
      url: `/api/app/bank-account-subtype/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });
}
