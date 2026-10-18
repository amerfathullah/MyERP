import type { NegativeBatchReportDto, NegativeBatchFilterDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class NegativeBatchReportService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: NegativeBatchFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NegativeBatchReportDto>({
      method: 'GET',
      url: '/api/app/negative-batch-report/report',
      params: {
        companyId: input.companyId,
        warehouseId: input.warehouseId,
        itemId: input.itemId,
        batchId: input.batchId,
        fromDate: input.fromDate,
        toDate: input.toDate,
        searchTerm: input.searchTerm,
      },
    },
    { apiName: this.apiName, ...config });
}
