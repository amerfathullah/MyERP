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
      url: '/api/app/item-group',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ItemGroupDto>>({
      method: 'GET',
      url: '/api/app/item-group',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}