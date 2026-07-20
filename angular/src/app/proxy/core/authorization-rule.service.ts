import type { AuthorizationRuleDto, CreateAuthorizationRuleDto, UpdateAuthorizationRuleDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AuthorizationRuleService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateAuthorizationRuleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AuthorizationRuleDto>({
      method: 'POST',
      url: '/api/app/authorization-rule',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/authorization-rule/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AuthorizationRuleDto>({
      method: 'GET',
      url: `/api/app/authorization-rule/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AuthorizationRuleDto>>({
      method: 'GET',
      url: '/api/app/authorization-rule',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateAuthorizationRuleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AuthorizationRuleDto>({
      method: 'PUT',
      url: `/api/app/authorization-rule/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}