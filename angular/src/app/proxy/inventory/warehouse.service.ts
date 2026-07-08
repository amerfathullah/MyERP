import type { CreateUpdateWarehouseDto, WarehouseDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class WarehouseService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateWarehouseDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WarehouseDto>({
      method: 'POST',
      url: '/api/app/warehouse',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/warehouse/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WarehouseDto>({
      method: 'GET',
      url: `/api/app/warehouse/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<WarehouseDto>>({
      method: 'GET',
      url: '/api/app/warehouse',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateWarehouseDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WarehouseDto>({
      method: 'PUT',
      url: `/api/app/warehouse/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}