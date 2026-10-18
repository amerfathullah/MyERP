import type {
  GetBilledItemsToBeReceivedInput,
  BilledItemsToBeReceivedReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BilledItemsToBeReceivedService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: GetBilledItemsToBeReceivedInput,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, BilledItemsToBeReceivedReportDto>(
      {
        method: 'GET',
        url: '/api/app/billed-items-to-be-received/report',
        params: {
          companyId: input.companyId,
          postingDate: input.postingDate,
          purchaseInvoiceId: input.purchaseInvoiceId,
          supplierId: input.supplierId,
          itemId: input.itemId,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
