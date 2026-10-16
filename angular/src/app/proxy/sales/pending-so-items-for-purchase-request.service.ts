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
      method: 'POST',
      url: '/api/app/pending-so-items-for-purchase-request/report',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
