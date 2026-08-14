import type { CreateUpdateItemManufacturerDto, ItemManufacturerDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemManufacturerService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateItemManufacturerDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemManufacturerDto>({
      method: 'POST',
      url: '/api/app/item-manufacturer',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/item-manufacturer/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemManufacturerDto>({
      method: 'GET',
      url: `/api/app/item-manufacturer/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ItemManufacturerDto>>({
      method: 'GET',
      url: '/api/app/item-manufacturer',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getListByItem = (itemId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemManufacturerDto[]>({
      method: 'GET',
      url: `/api/app/item-manufacturer/by-item/${itemId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateItemManufacturerDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemManufacturerDto>({
      method: 'PUT',
      url: `/api/app/item-manufacturer/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}