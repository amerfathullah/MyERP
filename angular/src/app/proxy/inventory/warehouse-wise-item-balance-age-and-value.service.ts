import type { WarehouseWiseItemBalanceAgeAndValueReportDto, WarehouseWiseItemBalanceAgeAndValueFilterDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class WarehouseWiseItemBalanceAgeAndValueService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: WarehouseWiseItemBalanceAgeAndValueFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WarehouseWiseItemBalanceAgeAndValueReportDto>({
      method: 'GET',
      url: '/api/app/warehouse-wise-item-balance-age-and-value/report',
      params: {
        companyId: input.companyId,
        warehouseId: input.warehouseId,
        itemId: input.itemId,
        itemGroupId: input.itemGroupId,
        fromDate: input.fromDate,
        toDate: input.toDate,
        filterTotalZeroQty: input.filterTotalZeroQty,
        searchTerm: input.searchTerm,
      },
    },
    { apiName: this.apiName, ...config });
}
