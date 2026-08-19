import type { CreateUpdateDriverDto, DriverDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class DriverService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateDriverDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DriverDto>({
      method: 'POST',
      url: '/api/app/driver',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/driver/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DriverDto>({
      method: 'GET',
      url: `/api/app/driver/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DriverDto>>({
      method: 'GET',
      url: '/api/app/driver',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  markLeft = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DriverDto>({
      method: 'POST',
      url: `/api/app/driver/${id}/mark-left`,
    },
    { apiName: this.apiName,...config });
  

  reinstate = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DriverDto>({
      method: 'POST',
      url: `/api/app/driver/${id}/reinstate`,
    },
    { apiName: this.apiName,...config });
  

  suspend = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DriverDto>({
      method: 'POST',
      url: `/api/app/driver/${id}/suspend`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateDriverDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DriverDto>({
      method: 'PUT',
      url: `/api/app/driver/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}