import type { CreateDunningDto, DunningDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class DunningService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateDunningDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DunningDto>({
      method: 'POST',
      url: '/api/app/dunning',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DunningDto>({
      method: 'GET',
      url: `/api/app/dunning/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DunningDto>>({
      method: 'GET',
      url: '/api/app/dunning',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  resolve = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DunningDto>({
      method: 'POST',
      url: `/api/app/dunning/${id}/resolve`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DunningDto>({
      method: 'POST',
      url: `/api/app/dunning/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}