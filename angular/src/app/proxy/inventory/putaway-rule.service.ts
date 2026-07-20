import type { CreateUpdatePutawayRuleDto, PutawayRuleDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class PutawayRuleService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdatePutawayRuleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PutawayRuleDto>({
      method: 'POST',
      url: '/api/app/putaway-rule',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/putaway-rule/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PutawayRuleDto>({
      method: 'GET',
      url: `/api/app/putaway-rule/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PutawayRuleDto>>({
      method: 'GET',
      url: '/api/app/putaway-rule',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  toggle = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/putaway-rule/${id}/toggle`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdatePutawayRuleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PutawayRuleDto>({
      method: 'PUT',
      url: `/api/app/putaway-rule/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}