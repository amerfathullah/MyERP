import type { BankGuaranteeDto, CreateUpdateBankGuaranteeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BankGuaranteeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankGuaranteeDto>({
      method: 'POST',
      url: `/api/app/bank-guarantee/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateBankGuaranteeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankGuaranteeDto>({
      method: 'POST',
      url: '/api/app/bank-guarantee',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/bank-guarantee/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankGuaranteeDto>({
      method: 'GET',
      url: `/api/app/bank-guarantee/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BankGuaranteeDto>>({
      method: 'GET',
      url: '/api/app/bank-guarantee',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankGuaranteeDto>({
      method: 'POST',
      url: `/api/app/bank-guarantee/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateBankGuaranteeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankGuaranteeDto>({
      method: 'PUT',
      url: `/api/app/bank-guarantee/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}