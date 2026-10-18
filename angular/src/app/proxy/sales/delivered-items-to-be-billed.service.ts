import type {
  GetDeliveredItemsToBeBilledInput,
  DeliveredItemsToBeBilledReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DeliveredItemsToBeBilledService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: GetDeliveredItemsToBeBilledInput,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, DeliveredItemsToBeBilledReportDto>(
      {
        method: 'GET',
        url: '/api/app/delivered-items-to-be-billed/report',
        params: {
          companyId: input.companyId,
          postingDate: input.postingDate,
          deliveryNoteId: input.deliveryNoteId,
          customerId: input.customerId,
          itemId: input.itemId,
        },
      },
      { apiName: this.apiName, ...config }
     );
}
