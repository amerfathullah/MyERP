import type {
  GetReceivedItemsToBeBilledInput,
  ReceivedItemsToBeBilledReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ReceivedItemsToBeBilledService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: GetReceivedItemsToBeBilledInput,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, ReceivedItemsToBeBilledReportDto>(
      {
        method: 'GET',
        url: '/api/app/received-items-to-be-billed/report',
        params: {
          companyId: input.companyId,
          postingDate: input.postingDate,
          purchaseReceiptId: input.purchaseReceiptId,
          supplierId: input.supplierId,
          itemId: input.itemId,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
