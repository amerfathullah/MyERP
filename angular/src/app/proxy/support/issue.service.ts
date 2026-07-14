import { Injectable } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { IssueDto, CreateIssueDto } from './models';
export { IssueDto, CreateIssueDto };

@Injectable({ providedIn: 'root' })
export class IssueService {
  apiName = 'Default';
  constructor(private restService: RestService) {}

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, IssueDto>({ method: 'GET', url: `/api/app/issue/${id}` }, { apiName: this.apiName, ...config });

  getList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<IssueDto>>({ method: 'GET', url: '/api/app/issue', params: { ...input } }, { apiName: this.apiName, ...config });

  create = (input: CreateIssueDto, config?: Partial<Rest.Config>) =>
    this.restService.request<CreateIssueDto, IssueDto>({ method: 'POST', url: '/api/app/issue', body: input }, { apiName: this.apiName, ...config });

  reply = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, IssueDto>({ method: 'POST', url: `/api/app/issue/${id}/reply` }, { apiName: this.apiName, ...config });

  resolve = (id: string, resolution?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssueDto>({ method: 'POST', url: `/api/app/issue/${id}/resolve`, params: { resolution } }, { apiName: this.apiName, ...config });

  reopen = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, IssueDto>({ method: 'POST', url: `/api/app/issue/${id}/reopen` }, { apiName: this.apiName, ...config });

  hold = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, IssueDto>({ method: 'POST', url: `/api/app/issue/${id}/hold` }, { apiName: this.apiName, ...config });
}
