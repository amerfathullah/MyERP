import type { CreateFinanceBookDto, FinanceBookDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class FinanceBookService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateFinanceBookDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FinanceBookDto>({
      method: 'POST',
      url: '/api/app/finance-book',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/finance-book/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FinanceBookDto>({
      method: 'GET',
      url: `/api/app/finance-book/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<FinanceBookDto>>({
      method: 'GET',
      url: '/api/app/finance-book',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  setDefault = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FinanceBookDto>({
      method: 'POST',
      url: `/api/app/finance-book/${id}/set-default`,
    },
    { apiName: this.apiName,...config });
}