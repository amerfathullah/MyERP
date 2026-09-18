import type { StockAgeingFilterDto, StockAgeingReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockAgeingService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: StockAgeingFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockAgeingReportDto>({
      method: 'GET',
      url: '/api/app/stock-ageing/report',
      params: {
        companyId: input.companyId,
        warehouseId: input.warehouseId,
        itemGroupId: input.itemGroupId,
        itemId: input.itemId,
        toDate: input.toDate,
        ranges: input.ranges,
        showWarehouseWiseStock: input.showWarehouseWiseStock,
        includeZeroStock: input.includeZeroStock,
        filterText: input.filterText,
      },
    },
    { apiName: this.apiName, ...config });
}
