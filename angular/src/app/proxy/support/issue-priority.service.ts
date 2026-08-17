import type { CreateUpdateIssuePriorityDto, GetIssuePriorityListDto, IssuePriorityDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class IssuePriorityService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateIssuePriorityDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssuePriorityDto>({
      method: 'POST',
      url: '/api/app/issue-priority',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/issue-priority/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssuePriorityDto>({
      method: 'GET',
      url: `/api/app/issue-priority/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetIssuePriorityListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<IssuePriorityDto>>({
      method: 'GET',
      url: '/api/app/issue-priority',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateIssuePriorityDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssuePriorityDto>({
      method: 'PUT',
      url: `/api/app/issue-priority/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}