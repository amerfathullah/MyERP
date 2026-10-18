import type { SerialAndBatchWiseStockBalanceFilterDto, SerialAndBatchWiseStockBalanceReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SerialAndBatchWiseStockBalanceService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: SerialAndBatchWiseStockBalanceFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SerialAndBatchWiseStockBalanceReportDto>(
      {
        method: 'GET',
        url: '/api/app/serial-and-batch-wise-stock-balance/report',
        params: {
          companyId: input.companyId,
          warehouseId: input.warehouseId,
          itemId: input.itemId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          includeZeroStockItems: input.includeZeroStockItems,
          searchTerm: input.searchTerm,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
