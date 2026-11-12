import type { CreateGlRepostingInputDto, CreateGlRepostingResultDto, StockGlComparisonDto, StockGlComparisonRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockGlComparisonService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  createGlRepostingEntries = (input: CreateGlRepostingInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CreateGlRepostingResultDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/stock-gl-comparison/gl-reposting-entries',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  getComparison = (input: StockGlComparisonRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockGlComparisonDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/stock-gl-comparison/comparison',
      params: { companyId: input.companyId, asOfDate: input.asOfDate },
    },
      { apiName: this.apiName,...config });
}
