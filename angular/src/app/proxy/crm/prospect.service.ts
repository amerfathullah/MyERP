import type { CreateProspectDto, ProspectDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class ProspectService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  addLead = (id: string, leadId: string, leadName?: string, email?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProspectDto>({
      method: 'POST',
      url: `/api/app/prospect/${id}/lead/${leadId}`,
      params: { leadName, email },
    },
    { apiName: this.apiName,...config });
  

  convertToCustomer = (id: string, customerId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProspectDto>({
      method: 'POST',
      url: `/api/app/prospect/${id}/convert-to-customer/${customerId}`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateProspectDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProspectDto>({
      method: 'POST',
      url: '/api/app/prospect',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/prospect/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProspectDto>({
      method: 'GET',
      url: `/api/app/prospect/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ProspectDto>>({
      method: 'GET',
      url: '/api/app/prospect',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateProspectDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProspectDto>({
      method: 'PUT',
      url: `/api/app/prospect/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}