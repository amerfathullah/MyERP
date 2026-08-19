import type { CreateUpdateShareholderDto, ShareholderDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class ShareholderService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateShareholderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShareholderDto>({
      method: 'POST',
      url: '/api/app/shareholder',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/shareholder/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShareholderDto>({
      method: 'GET',
      url: `/api/app/shareholder/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ShareholderDto>>({
      method: 'GET',
      url: '/api/app/shareholder',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateShareholderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShareholderDto>({
      method: 'PUT',
      url: `/api/app/shareholder/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}