import type { TotalStockSummaryReportDto, TotalStockSummaryFilterDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TotalStockSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: TotalStockSummaryFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TotalStockSummaryReportDto>({
      method: 'GET',
      url: '/api/app/total-stock-summary/report',
      params: {
        companyId: input.companyId,
        groupBy: input.groupBy,
        warehouseId: input.warehouseId,
        itemGroupId: input.itemGroupId,
        searchTerm: input.searchTerm,
      },
    },
    { apiName: this.apiName, ...config });
}
