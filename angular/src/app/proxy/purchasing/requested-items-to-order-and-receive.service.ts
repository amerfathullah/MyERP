import type {
  RequestedItemsFilterDto,
  RequestedItemsToOrderAndReceiveReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class RequestedItemsToOrderAndReceiveService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: RequestedItemsFilterDto,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, RequestedItemsToOrderAndReceiveReportDto>(
      {
        method: 'GET',
        url: '/api/app/requested-items-to-order-and-receive/report',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          materialRequestId: input.materialRequestId,
          itemId: input.itemId,
          warehouseId: input.warehouseId,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
