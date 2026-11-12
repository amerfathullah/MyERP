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
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/work-order-stock-report/report',
      params: { companyId: input.companyId, warehouseId: input.warehouseId, workOrderId: input.workOrderId, itemId: input.itemId, readyToBuildOnly: input.readyToBuildOnly },
    },
      { apiName: this.apiName,...config });
}
