import type { FirstResponseTimeReportDto, GetFirstResponseTimeReportRequestDto, GetLostOpportunityReportRequestDto, GetOpportunitySummaryBySalesStageRequestDto, LostOpportunityReportDto, OpportunitySummaryBySalesStageDto, PipelineOpportunityDto, SalesFunnelDto, SalesFunnelInputDto, SalesPipelineDashboardDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesPipelineService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getFirstResponseTimeReport = (input: GetFirstResponseTimeReportRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FirstResponseTimeReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/sales-pipeline/first-response-time-report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate },
    },
      { apiName: this.apiName,...config });
  

  
  getLostOpportunityReport = (input: GetLostOpportunityReportRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LostOpportunityReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/sales-pipeline/lost-opportunity-report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, territory: input.territory, lostReason: input.lostReason, opportunityFrom: input.opportunityFrom, partyId: input.partyId },
    },
      { apiName: this.apiName,...config });
  

  
  getOpportunitySummaryBySalesStage = (input: GetOpportunitySummaryBySalesStageRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunitySummaryBySalesStageDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/sales-pipeline/opportunity-summary-by-sales-stage',
      params: { companyId: input.companyId, basedOn: input.basedOn, dataBasedOn: input.dataBasedOn, opportunityType: input.opportunityType, opportunitySource: input.opportunitySource, statuses: input.statuses, fromDate: input.fromDate, toDate: input.toDate },
    },
      { apiName: this.apiName,...config });
  

  
  getPipelineData = (companyId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPipelineDashboardDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/sales-pipeline/pipeline-data',
      params: { companyId },
    },
      { apiName: this.apiName,...config });
  

  
  getSalesFunnel = (input: SalesFunnelInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesFunnelDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/sales-pipeline/sales-funnel',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate },
    },
      { apiName: this.apiName,...config });
  

  
  getTopOpportunities = (companyId?: string, maxCount: number = 10, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PipelineOpportunityDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/sales-pipeline/top-opportunities',
      params: { companyId, maxCount },
    },
      { apiName: this.apiName,...config });
}
