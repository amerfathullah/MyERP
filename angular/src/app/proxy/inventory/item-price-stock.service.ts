import type { ItemPriceStockReportDto, ItemPriceStockFilterDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemPriceStockService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: ItemPriceStockFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemPriceStockReportDto>({
      method: 'GET',
      url: '/api/app/item-price-stock/report',
      params: {
        companyId: input.companyId,
        warehouseId: input.warehouseId,
        itemId: input.itemId,
        itemGroupId: input.itemGroupId,
        sellingPriceListId: input.sellingPriceListId,
        buyingPriceListId: input.buyingPriceListId,
        searchTerm: input.searchTerm,
        inStockOnly: input.inStockOnly,
      },
    },
    { apiName: this.apiName, ...config });
}
