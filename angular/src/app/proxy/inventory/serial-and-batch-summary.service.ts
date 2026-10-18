import type { SerialAndBatchSummaryFilterDto, SerialAndBatchSummaryReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SerialAndBatchSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: SerialAndBatchSummaryFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SerialAndBatchSummaryReportDto>(
      {
        method: 'GET',
        url: '/api/app/serial-and-batch-summary/report',
        params: {
          companyId: input.companyId,
          voucherType: input.voucherType,
          voucherNo: input.voucherNo,
          itemId: input.itemId,
          warehouseId: input.warehouseId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          serialNo: input.serialNo,
          batchId: input.batchId,
          searchTerm: input.searchTerm,
          includeCancelled: input.includeCancelled,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
