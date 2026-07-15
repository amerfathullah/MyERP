import type { CreateUpdateSupplierDto, GetSupplierListDto, SupplierDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SupplierService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateSupplierDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierDto>({
      method: 'POST',
      url: '/api/app/supplier',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/supplier/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierDto>({
      method: 'GET',
      url: `/api/app/supplier/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetSupplierListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SupplierDto>>({
      method: 'GET',
      url: '/api/app/supplier',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateSupplierDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierDto>({
      method: 'PUT',
      url: `/api/app/supplier/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}