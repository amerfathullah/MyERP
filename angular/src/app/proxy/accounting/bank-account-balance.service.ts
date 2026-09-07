import type { BankAccountBalanceDto, CreateUpdateBankAccountBalanceDto, GetBankAccountBalanceListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BankAccountBalanceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateBankAccountBalanceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankAccountBalanceDto>({
      method: 'POST',
      url: '/api/app/bank-account-balance',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/bank-account-balance/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankAccountBalanceDto>({
      method: 'GET',
      url: `/api/app/bank-account-balance/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAllList = (bankAccountId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankAccountBalanceDto[]>({
      method: 'GET',
      url: `/api/app/bank-account-balance/list/${bankAccountId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetBankAccountBalanceListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BankAccountBalanceDto>>({
      method: 'GET',
      url: '/api/app/bank-account-balance',
      params: { bankAccountId: input.bankAccountId, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateBankAccountBalanceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankAccountBalanceDto>({
      method: 'PUT',
      url: `/api/app/bank-account-balance/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}