import type { PipelineOpportunityDto, SalesPipelineDashboardDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesPipelineService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getPipelineData = (companyId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPipelineDashboardDto>({
      method: 'GET',
      url: '/api/app/sales-pipeline/pipeline-data',
      params: { companyId },
    },
    { apiName: this.apiName,...config });
  

  getTopOpportunities = (companyId?: string, maxCount: number = 10, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PipelineOpportunityDto[]>({
      method: 'GET',
      url: '/api/app/sales-pipeline/top-opportunities',
      params: { companyId, maxCount },
    },
    { apiName: this.apiName,...config });
}