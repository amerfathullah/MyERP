import type { CreateIssueDto, GetIssueListDto, IssueDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class IssueService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateIssueDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssueDto>({
      method: 'POST',
      url: '/api/app/issue',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssueDto>({
      method: 'GET',
      url: `/api/app/issue/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetIssueListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<IssueDto>>({
      method: 'GET',
      url: '/api/app/issue',
      params: { status: input.status, companyId: input.companyId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  hold = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssueDto>({
      method: 'POST',
      url: `/api/app/issue/${id}/hold`,
    },
    { apiName: this.apiName,...config });
  

  reopen = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssueDto>({
      method: 'POST',
      url: `/api/app/issue/${id}/reopen`,
    },
    { apiName: this.apiName,...config });
  

  reply = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssueDto>({
      method: 'POST',
      url: `/api/app/issue/${id}/reply`,
    },
    { apiName: this.apiName,...config });
  

  resolve = (id: string, resolution?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssueDto>({
      method: 'POST',
      url: `/api/app/issue/${id}/resolve`,
      params: { resolution },
    },
    { apiName: this.apiName,...config });
}