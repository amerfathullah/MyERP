import type { WarehouseWiseStockBalanceFilterDto, WarehouseWiseStockBalanceReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class WarehouseWiseStockBalanceService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: WarehouseWiseStockBalanceFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WarehouseWiseStockBalanceReportDto>({
      method: 'GET',
      url: '/api/app/warehouse-wise-stock-balance/report',
      params: {
        companyId: input.companyId,
        warehouseId: input.warehouseId,
        showDisabledWarehouses: input.showDisabledWarehouses,
        asOfDate: input.asOfDate,
      },
    },
    { apiName: this.apiName, ...config });
}
