import { Injectable } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';

export interface StockBalanceDto {
  id?: string;
  itemId?: string;
  warehouseId?: string;
  actualQty?: number;
  orderedQty?: number;
  plannedQty?: number;
  reservedQty?: number;
  indentedQty?: number;
  projectedQty?: number;
  stockValue?: number;
  valuationRate?: number;
}

@Injectable({ providedIn: 'root' })
export class StockBalanceService {
  apiName = 'Default';

  constructor(private restService: RestService) {}

  getStockBalance = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<StockBalanceDto>>({ method: 'GET', url: '/api/app/stock-balance/stock-balance', params: { ...input } }, { apiName: this.apiName, ...config });

  getItemStock = (itemId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, StockBalanceDto[]>({ method: 'GET', url: `/api/app/stock-balance/item-stock/${itemId}` }, { apiName: this.apiName, ...config });
}
