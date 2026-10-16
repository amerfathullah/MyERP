import type {
  GetItemWisePurchaseHistoryDto,
  ItemWisePurchaseHistoryReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemWisePurchaseHistoryService {
  private restService = inject(RestService);
  apiName = 'Default';

  getHistory = (input: GetItemWisePurchaseHistoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemWisePurchaseHistoryReportDto>(
      {
        method: 'GET',
        url: '/api/app/item-wise-purchase-history/history',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          supplierId: input.supplierId,
          itemId: input.itemId,
          itemCode: input.itemCode,
          itemGroupId: input.itemGroupId,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
