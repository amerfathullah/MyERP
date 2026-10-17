import type { WorkOrderStockReportDto, WorkOrderStockReportRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class WorkOrderStockReportService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: WorkOrderStockReportRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderStockReportDto>({
      method: 'POST',
      url: '/api/app/work-order-stock-report/report',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
