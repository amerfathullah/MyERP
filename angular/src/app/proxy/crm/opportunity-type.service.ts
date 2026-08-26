import type { OpportunityTypeDto, CreateUpdateOpportunityTypeDto, GetOpportunityTypeListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class OpportunityTypeService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreateUpdateOpportunityTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityTypeDto>({
      method: 'POST',
      url: '/api/app/opportunity-type',
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/opportunity-type/${id}`,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityTypeDto>({
      method: 'GET',
      url: `/api/app/opportunity-type/${id}`,
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetOpportunityTypeListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<OpportunityTypeDto>>({
      method: 'GET',
      url: '/api/app/opportunity-type',
      params: { filter: input.filter, isActive: input.isActive, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateOpportunityTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityTypeDto>({
      method: 'PUT',
      url: `/api/app/opportunity-type/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });
}
