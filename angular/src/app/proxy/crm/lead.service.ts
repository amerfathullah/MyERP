import { Injectable, inject } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import type { LeadDto, CreateLeadDto, UpdateLeadDto, GetLeadListDto, ConvertLeadToOpportunityDto, OpportunityDto } from './models';

@Injectable({ providedIn: 'root' })
export class LeadService {
  apiName = 'Default';

  private restService = inject(RestService);

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, LeadDto>({ method: 'GET', url: `/api/app/lead/${id}` }, { apiName: this.apiName, ...config });

  getList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LeadDto>>({ method: 'GET', url: '/api/app/lead', params: { ...input } }, { apiName: this.apiName, ...config });

  create = (input: CreateLeadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<CreateLeadDto, LeadDto>({ method: 'POST', url: '/api/app/lead', body: input }, { apiName: this.apiName, ...config });

  update = (id: string, input: UpdateLeadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<UpdateLeadDto, LeadDto>({ method: 'PUT', url: `/api/app/lead/${id}`, body: input }, { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, void>({ method: 'DELETE', url: `/api/app/lead/${id}` }, { apiName: this.apiName, ...config });

  qualify = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, LeadDto>({ method: 'POST', url: `/api/app/lead/${id}/qualify` }, { apiName: this.apiName, ...config });

  markLost = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, LeadDto>({ method: 'POST', url: `/api/app/lead/${id}/mark-lost` }, { apiName: this.apiName, ...config });

  convertToOpportunity = (input: ConvertLeadToOpportunityDto, config?: Partial<Rest.Config>) =>
    this.restService.request<ConvertLeadToOpportunityDto, OpportunityDto>({ method: 'POST', url: '/api/app/lead/convert-to-opportunity', body: input }, { apiName: this.apiName, ...config });
}


