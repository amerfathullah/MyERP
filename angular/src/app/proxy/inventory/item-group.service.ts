import type { CreateItemGroupDto, ItemGroupDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemGroupService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  create = (input: CreateItemGroupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemGroupDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/item-group',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/item-group/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemGroupDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/item-group/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ItemGroupDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/item-group',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: CreateItemGroupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemGroupDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/app/item-group/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
