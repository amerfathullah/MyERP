import type { BatchItemExpiryStatusRequestDto, BatchItemExpiryStatusReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BatchItemExpiryStatusService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: BatchItemExpiryStatusRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchItemExpiryStatusReportDto>({
      method: 'GET',
      url: '/api/app/batch-item-expiry-status/report',
      params: {
        fromDate: input.fromDate,
        toDate: input.toDate,
        companyId: input.companyId,
        itemId: input.itemId,
        warehouseId: input.warehouseId,
        searchText: input.searchText,
      },
    },
    { apiName: this.apiName, ...config });
}
