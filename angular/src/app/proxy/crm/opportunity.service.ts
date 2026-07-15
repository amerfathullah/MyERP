import { Injectable, inject } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import type { OpportunityDto, CreateOpportunityDto, UpdateOpportunityDto } from './models';

@Injectable({ providedIn: 'root' })
export class OpportunityService {
  apiName = 'Default';

  private restService = inject(RestService);

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, OpportunityDto>({ method: 'GET', url: `/api/app/opportunity/${id}` }, { apiName: this.apiName, ...config });

  getList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<OpportunityDto>>({ method: 'GET', url: '/api/app/opportunity', params: { ...input } }, { apiName: this.apiName, ...config });

  create = (input: CreateOpportunityDto, config?: Partial<Rest.Config>) =>
    this.restService.request<CreateOpportunityDto, OpportunityDto>({ method: 'POST', url: '/api/app/opportunity', body: input }, { apiName: this.apiName, ...config });

  update = (id: string, input: UpdateOpportunityDto, config?: Partial<Rest.Config>) =>
    this.restService.request<UpdateOpportunityDto, OpportunityDto>({ method: 'PUT', url: `/api/app/opportunity/${id}`, body: input }, { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, void>({ method: 'DELETE', url: `/api/app/opportunity/${id}` }, { apiName: this.apiName, ...config });

  markQuotation = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, OpportunityDto>({ method: 'POST', url: `/api/app/opportunity/${id}/mark-quotation` }, { apiName: this.apiName, ...config });

  convert = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, OpportunityDto>({ method: 'POST', url: `/api/app/opportunity/${id}/convert` }, { apiName: this.apiName, ...config });

  declareLost = (id: string, reason?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityDto>({ method: 'POST', url: `/api/app/opportunity/${id}/declare-lost`, params: { reason } }, { apiName: this.apiName, ...config });

  close = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, OpportunityDto>({ method: 'POST', url: `/api/app/opportunity/${id}/close` }, { apiName: this.apiName, ...config });

  reopen = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, OpportunityDto>({ method: 'POST', url: `/api/app/opportunity/${id}/reopen` }, { apiName: this.apiName, ...config });
}


