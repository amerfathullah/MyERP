import type { CreateUpdateItemPriceDto, CreateUpdatePriceListDto, GetItemPriceListDto, GetItemRateRequestDto, ItemPriceDto, ItemRateResultDto, PriceListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PricingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  createItemPrice = (input: CreateUpdateItemPriceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemPriceDto>({
      method: 'POST',
      url: '/api/app/pricing/item-price',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createPriceList = (input: CreateUpdatePriceListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PriceListDto>({
      method: 'POST',
      url: '/api/app/pricing/price-list',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  deleteItemPrice = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/pricing/${id}/item-price`,
    },
    { apiName: this.apiName,...config });
  

  getItemPrices = (input: GetItemPriceListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ItemPriceDto>>({
      method: 'GET',
      url: '/api/app/pricing/item-prices',
      params: { itemId: input.itemId, priceListId: input.priceListId, customerId: input.customerId, supplierId: input.supplierId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getItemRate = (input: GetItemRateRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemRateResultDto>({
      method: 'GET',
      url: '/api/app/pricing/item-rate',
      params: { itemId: input.itemId, priceListId: input.priceListId, qty: input.qty, transactionDate: input.transactionDate, customerId: input.customerId, supplierId: input.supplierId, batchNo: input.batchNo },
    },
    { apiName: this.apiName,...config });
  

  getPriceLists = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, PriceListDto[]>({
      method: 'GET',
      url: '/api/app/pricing/price-lists',
    },
    { apiName: this.apiName,...config });
  

  updateItemPrice = (id: string, input: CreateUpdateItemPriceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemPriceDto>({
      method: 'PUT',
      url: `/api/app/pricing/${id}/item-price`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updatePriceList = (id: string, input: CreateUpdatePriceListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PriceListDto>({
      method: 'PUT',
      url: `/api/app/pricing/${id}/price-list`,
      body: input,
    },
    { apiName: this.apiName,...config });
}