import type { BomStockAnalysisDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BomStockAnalysisService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getAnalysis = (bomId: string, requiredQty: number = 1, warehouseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomStockAnalysisDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/bom-stock-analysis/analysis',
      params: { bomId, requiredQty, warehouseId },
    },
      { apiName: this.apiName,...config });
}
