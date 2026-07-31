import type { InventoryTurnoverReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class InventoryTurnoverService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getReport = (companyId: string, fromDate: string, toDate: string, warehouseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InventoryTurnoverReportDto>({
      method: 'GET',
      url: '/api/app/inventory-turnover/report',
      params: { companyId, fromDate, toDate, warehouseId },
    },
    { apiName: this.apiName,...config });
}