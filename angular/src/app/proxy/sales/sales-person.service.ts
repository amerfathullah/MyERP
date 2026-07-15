import type { CreateSalesPersonDto, CreateSalesTargetDto, SalesPersonDto, UpdateSalesPersonDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesPersonService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  addTarget = (id: string, input: CreateSalesTargetDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/sales-person/${id}/target`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateSalesPersonDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPersonDto>({
      method: 'POST',
      url: '/api/app/sales-person',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/sales-person/${id}`,
    },
    { apiName: this.apiName,...config });
  

  disable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/sales-person/${id}/disable`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPersonDto>({
      method: 'GET',
      url: `/api/app/sales-person/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SalesPersonDto>>({
      method: 'GET',
      url: '/api/app/sales-person',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTree = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPersonDto[]>({
      method: 'GET',
      url: '/api/app/sales-person/tree',
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateSalesPersonDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPersonDto>({
      method: 'PUT',
      url: `/api/app/sales-person/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}