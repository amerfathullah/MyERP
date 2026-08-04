import type { BulkPriceUpdateDto, BulkPriceUpdateResultDto, CreateUpdateItemPriceDto, GetItemPriceListDto, ItemPriceDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemPriceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  bulkUpdate = (input: BulkPriceUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BulkPriceUpdateResultDto>({
      method: 'POST',
      url: '/api/app/item-price/bulk-update',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateItemPriceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemPriceDto>({
      method: 'POST',
      url: '/api/app/item-price',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/item-price/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemPriceDto>({
      method: 'GET',
      url: `/api/app/item-price/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetItemPriceListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ItemPriceDto>>({
      method: 'GET',
      url: '/api/app/item-price',
      params: { itemId: input.itemId, priceListId: input.priceListId, customerId: input.customerId, supplierId: input.supplierId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateItemPriceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemPriceDto>({
      method: 'PUT',
      url: `/api/app/item-price/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}