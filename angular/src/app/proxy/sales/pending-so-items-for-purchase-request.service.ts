import type { PendingSoItemsForPurchaseRequestReportDto, PendingSoItemsForPurchaseRequestRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PendingSoItemsForPurchaseRequestService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: PendingSoItemsForPurchaseRequestRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PendingSoItemsForPurchaseRequestReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/pending-so-items-for-purchase-request/report',
      params: { companyId: input.companyId, customerId: input.customerId, salesOrderId: input.salesOrderId, itemId: input.itemId, fromDate: input.fromDate, toDate: input.toDate },
    },
      { apiName: this.apiName,...config });
}
