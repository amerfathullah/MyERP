import type { AutoRepeatDto, CreateAutoRepeatDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AutoRepeatService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateAutoRepeatDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AutoRepeatDto>({
      method: 'POST',
      url: '/api/app/auto-repeat',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/auto-repeat/${id}`,
    },
    { apiName: this.apiName,...config });
  

  disable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/auto-repeat/${id}/disable`,
    },
    { apiName: this.apiName,...config });
  

  enable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/auto-repeat/${id}/enable`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AutoRepeatDto>({
      method: 'GET',
      url: `/api/app/auto-repeat/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AutoRepeatDto>>({
      method: 'GET',
      url: '/api/app/auto-repeat',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}