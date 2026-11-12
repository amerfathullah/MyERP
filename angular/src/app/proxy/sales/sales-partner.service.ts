import type { CreateSalesPartnerDto, CreateSalesPartnerTargetDto, GetSalesPartnerListDto, PublicSalesPartnerDto, SalesPartnerDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesPartnerService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  addTarget = (id: string, input: CreateSalesPartnerTargetDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPartnerDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/sales-partner/${id}/target`,
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  create = (input: CreateSalesPartnerDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPartnerDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/sales-partner',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/sales-partner/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPartnerDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/sales-partner/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: GetSalesPartnerListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SalesPartnerDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/sales-partner',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  getPublishedPartners = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PublicSalesPartnerDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/sales-partner/published-partners',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  removeTarget = (id: string, targetId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPartnerDto>({
      method: 'DELETE',
      headers: { Accept: 'application/json' },
      url: `/api/app/sales-partner/${id}/target/${targetId}`,
    },
      { apiName: this.apiName,...config });
  

  
  toggle = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/sales-partner/${id}/toggle`,
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: CreateSalesPartnerDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPartnerDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/app/sales-partner/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
