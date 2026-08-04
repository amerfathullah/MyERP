import type { CreateItemVariantDto, CreateUpdateItemDto, GetItemListDto, ItemDto, ItemPriceHistoryDto, ItemStockMovementDto, ItemTransactionSummaryDto, ItemVariantDto, ItemWhereUsedDto, ReorderSuggestionDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateItemDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemDto>({
      method: 'POST',
      url: '/api/app/item',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createVariant = (templateItemId: string, input: CreateItemVariantDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemDto>({
      method: 'POST',
      url: `/api/app/item/variant/${templateItemId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/item/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemDto>({
      method: 'GET',
      url: `/api/app/item/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetItemListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ItemDto>>({
      method: 'GET',
      url: '/api/app/item',
      params: { filter: input.filter, companyId: input.companyId, itemType: input.itemType, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getPriceHistory = (itemId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemPriceHistoryDto[]>({
      method: 'GET',
      url: `/api/app/item/price-history/${itemId}`,
    },
    { apiName: this.apiName,...config });
  

  getRecentMovements = (itemId: string, maxCount: number = 20, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemStockMovementDto[]>({
      method: 'GET',
      url: `/api/app/item/recent-movements/${itemId}`,
      params: { maxCount },
    },
    { apiName: this.apiName,...config });
  

  getReorderSuggestions = (companyId: string, lookbackDays: number = 90, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReorderSuggestionDto[]>({
      method: 'GET',
      url: `/api/app/item/reorder-suggestions/${companyId}`,
      params: { lookbackDays },
    },
    { apiName: this.apiName,...config });
  

  getTransactionSummary = (itemId: string, companyId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemTransactionSummaryDto>({
      method: 'GET',
      url: '/api/app/item/transaction-summary',
      params: { itemId, companyId },
    },
    { apiName: this.apiName,...config });
  

  getVariants = (templateItemId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemVariantDto[]>({
      method: 'GET',
      url: `/api/app/item/variants/${templateItemId}`,
    },
    { apiName: this.apiName,...config });
  

  getWhereUsed = (itemId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemWhereUsedDto[]>({
      method: 'GET',
      url: `/api/app/item/where-used/${itemId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateItemDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemDto>({
      method: 'PUT',
      url: `/api/app/item/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}