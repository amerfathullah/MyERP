import type { BankDto, CreateUpdateBankDto, GetBankListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BankService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreateUpdateBankDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankDto>({
      method: 'POST',
      url: '/api/app/bank',
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/bank/${id}`,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankDto>({
      method: 'GET',
      url: `/api/app/bank/${id}`,
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetBankListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BankDto>>({
      method: 'GET',
      url: '/api/app/bank',
      params: { filter: input.filter, isActive: input.isActive, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateBankDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankDto>({
      method: 'PUT',
      url: `/api/app/bank/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });
}
