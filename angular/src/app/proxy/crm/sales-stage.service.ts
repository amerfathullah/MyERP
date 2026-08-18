import type { CreateUpdateSalesStageDto, GetSalesStageListDto, SalesStageDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesStageService {
  private restService = inject(RestService);
  apiName = 'Default';


  create = (input: CreateUpdateSalesStageDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesStageDto>({
      method: 'POST',
      url: '/api/app/sales-stage',
      body: input,
    },
    { apiName: this.apiName,...config });


  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/sales-stage/${id}`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesStageDto>({
      method: 'GET',
      url: `/api/app/sales-stage/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: GetSalesStageListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SalesStageDto>>({
      method: 'GET',
      url: '/api/app/sales-stage',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  update = (id: string, input: CreateUpdateSalesStageDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesStageDto>({
      method: 'PUT',
      url: `/api/app/sales-stage/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}
