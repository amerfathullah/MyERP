import type { ReservedStockReportDto, ReservedStockFilterDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ReservedStockReportService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: ReservedStockFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReservedStockReportDto>({
      method: 'GET',
      url: '/api/app/reserved-stock-report/report',
      params: {
        companyId: input.companyId,
        fromDate: input.fromDate,
        toDate: input.toDate,
        itemId: input.itemId,
        warehouseId: input.warehouseId,
        voucherType: input.voucherType,
        status: input.status,
        searchTerm: input.searchTerm,
      },
    },
    { apiName: this.apiName, ...config });
}
