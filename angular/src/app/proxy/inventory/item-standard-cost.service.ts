import type { CreateItemStandardCostDto, GetItemStandardCostListDto, ItemStandardCostDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemStandardCostService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemStandardCostDto>({
      method: 'POST',
      url: `/api/app/item-standard-cost/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateItemStandardCostDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemStandardCostDto>({
      method: 'POST',
      url: '/api/app/item-standard-cost',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemStandardCostDto>({
      method: 'GET',
      url: `/api/app/item-standard-cost/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getCurrent = (itemId: string, companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemStandardCostDto>({
      method: 'GET',
      url: '/api/app/item-standard-cost/current',
      params: { itemId, companyId },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetItemStandardCostListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ItemStandardCostDto>>({
      method: 'GET',
      url: '/api/app/item-standard-cost',
      params: { itemId: input.itemId, companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemStandardCostDto>({
      method: 'POST',
      url: `/api/app/item-standard-cost/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}