import type { CreateFiscalYearDto, FiscalYearDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class FiscalYearService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  close = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FiscalYearDto>({
      method: 'POST',
      url: `/api/app/fiscal-year/${id}/close`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateFiscalYearDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FiscalYearDto>({
      method: 'POST',
      url: '/api/app/fiscal-year',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FiscalYearDto>({
      method: 'GET',
      url: `/api/app/fiscal-year/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getCurrent = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FiscalYearDto>({
      method: 'GET',
      url: `/api/app/fiscal-year/current/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<FiscalYearDto>>({
      method: 'GET',
      url: '/api/app/fiscal-year',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}