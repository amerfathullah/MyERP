import type { ItemWiseConsumptionRequestDto, ItemWiseConsumptionReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemWiseConsumptionService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: ItemWiseConsumptionRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemWiseConsumptionReportDto>({
      method: 'GET',
      url: '/api/app/item-wise-consumption/report',
      params: {
        fromDate: input.fromDate,
        toDate: input.toDate,
        companyId: input.companyId,
        itemId: input.itemId,
        warehouseId: input.warehouseId,
        supplierId: input.supplierId,
        searchText: input.searchText,
      },
    },
    { apiName: this.apiName, ...config });
}
