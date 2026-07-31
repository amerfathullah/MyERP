import type { BomStockAnalysisDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BomStockAnalysisService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getAnalysis = (bomId: string, requiredQty: number = 1, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomStockAnalysisDto>({
      method: 'GET',
      url: `/api/app/bom-stock-analysis/analysis/${bomId}`,
      params: { requiredQty },
    },
    { apiName: this.apiName,...config });
}