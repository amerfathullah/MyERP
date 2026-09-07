import type { CreateUpdateSalesPartnerTypeDto, GetSalesPartnerTypeListDto, SalesPartnerTypeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesPartnerTypeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateSalesPartnerTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPartnerTypeDto>({
      method: 'POST',
      url: '/api/app/sales-partner-type',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/sales-partner-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPartnerTypeDto>({
      method: 'GET',
      url: `/api/app/sales-partner-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetSalesPartnerTypeListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SalesPartnerTypeDto>>({
      method: 'GET',
      url: '/api/app/sales-partner-type',
      params: { filter: input.filter, isActive: input.isActive, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateSalesPartnerTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPartnerTypeDto>({
      method: 'PUT',
      url: `/api/app/sales-partner-type/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}