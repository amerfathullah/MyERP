import type { StockGlComparisonDto, StockGlComparisonRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockGlComparisonService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getComparison = (input: StockGlComparisonRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockGlComparisonDto>({
      method: 'GET',
      url: '/api/app/stock-gl-comparison/comparison',
      params: { companyId: input.companyId, asOfDate: input.asOfDate },
    },
    { apiName: this.apiName,...config });
}