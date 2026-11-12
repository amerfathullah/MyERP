import type { IssuedItemsAgainstWorkOrderReportDto, IssuedItemsAgainstWorkOrderRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class IssuedItemsAgainstWorkOrderService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: IssuedItemsAgainstWorkOrderRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssuedItemsAgainstWorkOrderReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/issued-items-against-work-order/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, workOrderId: input.workOrderId, itemId: input.itemId, sourceWarehouseId: input.sourceWarehouseId },
    },
      { apiName: this.apiName,...config });
}
