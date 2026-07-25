import type { GetItemsAvailabilityInput, GetStockBalanceRequestDto, ItemAvailabilityDto, StockBalanceDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockBalanceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getItemStock = (itemId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockBalanceDto[]>({
      method: 'GET',
      url: `/api/app/stock-balance/item-stock/${itemId}`,
    },
    { apiName: this.apiName,...config });
  

  getItemsAvailability = (input: GetItemsAvailabilityInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemAvailabilityDto[]>({
      method: 'GET',
      url: '/api/app/stock-balance/items-availability',
      params: { itemIds: input.itemIds, warehouseId: input.warehouseId },
    },
    { apiName: this.apiName,...config });
  

  getStockBalance = (input: GetStockBalanceRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<StockBalanceDto>>({
      method: 'GET',
      url: '/api/app/stock-balance/stock-balance',
      params: { itemId: input.itemId, warehouseId: input.warehouseId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}