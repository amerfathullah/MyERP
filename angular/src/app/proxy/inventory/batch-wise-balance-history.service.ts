import type { BatchWiseBalanceHistoryFilterDto, BatchWiseBalanceHistoryReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BatchWiseBalanceHistoryService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: BatchWiseBalanceHistoryFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchWiseBalanceHistoryReportDto>(
      {
        method: 'GET',
        url: '/api/app/batch-wise-balance-history/report',
        params: {
          companyId: input.companyId,
          warehouseId: input.warehouseId,
          warehouseType: input.warehouseType,
          itemId: input.itemId,
          batchId: input.batchId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          includeZeroStockBatches: input.includeZeroStockBatches,
          searchTerm: input.searchTerm,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
