import type { CreateUpdateItemLeadTimeDto, GetItemLeadTimeListDto, ItemLeadTimeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemLeadTimeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateItemLeadTimeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemLeadTimeDto>({
      method: 'POST',
      url: '/api/app/item-lead-time',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/item-lead-time/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemLeadTimeDto>({
      method: 'GET',
      url: `/api/app/item-lead-time/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getByItemId = (itemId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemLeadTimeDto>({
      method: 'GET',
      url: `/api/app/item-lead-time/by-item-id/${itemId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetItemLeadTimeListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ItemLeadTimeDto>>({
      method: 'GET',
      url: '/api/app/item-lead-time',
      params: { filter: input.filter, itemId: input.itemId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateItemLeadTimeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemLeadTimeDto>({
      method: 'PUT',
      url: `/api/app/item-lead-time/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}