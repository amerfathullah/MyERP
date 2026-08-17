import type { CreateUpdateIssueTypeDto, GetIssueTypeListDto, IssueTypeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class IssueTypeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateIssueTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssueTypeDto>({
      method: 'POST',
      url: '/api/app/issue-type',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/issue-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssueTypeDto>({
      method: 'GET',
      url: `/api/app/issue-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetIssueTypeListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<IssueTypeDto>>({
      method: 'GET',
      url: '/api/app/issue-type',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateIssueTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssueTypeDto>({
      method: 'PUT',
      url: `/api/app/issue-type/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}