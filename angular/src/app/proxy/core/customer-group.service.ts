import type { CreateUpdateCustomerGroupDto, CustomerGroupDto, GetCustomerGroupListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CustomerGroupService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreateUpdateCustomerGroupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerGroupDto>({
      method: 'POST',
      url: '/api/app/customer-group',
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/customer-group/${id}`,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerGroupDto>({
      method: 'GET',
      url: `/api/app/customer-group/${id}`,
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetCustomerGroupListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CustomerGroupDto>>({
      method: 'GET',
      url: '/api/app/customer-group',
      params: { parentId: input.parentId, isGroup: input.isGroup, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateCustomerGroupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerGroupDto>({
      method: 'PUT',
      url: `/api/app/customer-group/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });
}
