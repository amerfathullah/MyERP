import type { RequestedItemsToTransferFilterDto, RequestedItemsToTransferReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class RequestedItemsToTransferService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: RequestedItemsToTransferFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RequestedItemsToTransferReportDto>({
      method: 'GET',
      url: '/api/app/requested-items-to-transfer/report',
      params: {
        companyId: input.companyId,
        fromDate: input.fromDate,
        toDate: input.toDate,
        itemId: input.itemId,
        warehouseId: input.warehouseId,
        searchText: input.searchText,
      },
    },
    { apiName: this.apiName, ...config });
}
