import type { SerialNoAndBatchTraceabilityFilterDto, SerialNoAndBatchTraceabilityReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SerialNoAndBatchTraceabilityService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: SerialNoAndBatchTraceabilityFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SerialNoAndBatchTraceabilityReportDto>(
      {
        method: 'GET',
        url: '/api/app/serial-no-and-batch-traceability/report',
        params: {
          companyId: input.companyId,
          itemId: input.itemId,
          itemCode: input.itemCode,
          batchId: input.batchId,
          batchNo: input.batchNo,
          serialNo: input.serialNo,
          traceabilityDirection: input.traceabilityDirection,
          fromDate: input.fromDate,
          toDate: input.toDate,
          searchTerm: input.searchTerm,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
