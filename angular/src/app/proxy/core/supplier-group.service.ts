import type { CreateUpdateSupplierGroupDto, GetSupplierGroupListDto, SupplierGroupDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SupplierGroupService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreateUpdateSupplierGroupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierGroupDto>({
      method: 'POST',
      url: '/api/app/supplier-group',
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/supplier-group/${id}`,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierGroupDto>({
      method: 'GET',
      url: `/api/app/supplier-group/${id}`,
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetSupplierGroupListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SupplierGroupDto>>({
      method: 'GET',
      url: '/api/app/supplier-group',
      params: { parentId: input.parentId, isGroup: input.isGroup, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateSupplierGroupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierGroupDto>({
      method: 'PUT',
      url: `/api/app/supplier-group/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });
}
