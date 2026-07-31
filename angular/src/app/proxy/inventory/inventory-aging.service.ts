import type { InventoryAgingReportDto, InventoryAgingRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class InventoryAgingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getReport = (input: InventoryAgingRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InventoryAgingReportDto>({
      method: 'GET',
      url: '/api/app/inventory-aging/report',
      params: { companyId: input.companyId, slowMovingDays: input.slowMovingDays, deadStockDays: input.deadStockDays },
    },
    { apiName: this.apiName,...config });
}