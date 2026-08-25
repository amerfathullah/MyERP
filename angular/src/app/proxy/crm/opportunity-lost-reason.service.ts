import type { CreateUpdateOpportunityLostReasonDto, GetOpportunityLostReasonListDto, OpportunityLostReasonDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class OpportunityLostReasonService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreateUpdateOpportunityLostReasonDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityLostReasonDto>({
      method: 'POST',
      url: '/api/app/opportunity-lost-reason',
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/opportunity-lost-reason/${id}`,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityLostReasonDto>({
      method: 'GET',
      url: `/api/app/opportunity-lost-reason/${id}`,
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetOpportunityLostReasonListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<OpportunityLostReasonDto>>({
      method: 'GET',
      url: '/api/app/opportunity-lost-reason',
      params: { companyId: input.companyId, isDisabled: input.isDisabled, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateOpportunityLostReasonDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityLostReasonDto>({
      method: 'PUT',
      url: `/api/app/opportunity-lost-reason/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });
}
