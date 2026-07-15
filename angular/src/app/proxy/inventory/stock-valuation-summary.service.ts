import type { StockValuationSummaryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockValuationSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getSummary = (companyId: string, warehouseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockValuationSummaryDto>({
      method: 'GET',
      url: '/api/app/stock-valuation-summary/summary',
      params: { companyId, warehouseId },
    },
    { apiName: this.apiName,...config });
}