import type { AvailableBatchReportDto, AvailableBatchFilterDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AvailableBatchReportService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: AvailableBatchFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AvailableBatchReportDto>({
      method: 'GET',
      url: '/api/app/available-batch-report/report',
      params: {
        companyId: input.companyId,
        toDate: input.toDate,
        itemId: input.itemId,
        warehouseId: input.warehouseId,
        warehouseType: input.warehouseType,
        batchId: input.batchId,
        includeExpiredBatches: input.includeExpiredBatches,
        searchTerm: input.searchTerm,
      },
    },
    { apiName: this.apiName, ...config });
}
