import type {
  CrmReportDateFilterDto,
  LeadConversionTimeReportDto,
  LeadDetailsFilterDto,
  LeadDetailsReportDto,
  ProspectsEngagedFilterDto,
  ProspectsEngagedReportDto,
  SalesPipelineAnalyticsFilterDto,
  SalesPipelineAnalyticsReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CrmReportService {
  private restService = inject(RestService);
  apiName = 'Default';

  getLeadConversionTime = (input: CrmReportDateFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeadConversionTimeReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/crm-report/lead-conversion-time',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getLeadDetails = (input: LeadDetailsFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeadDetailsReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/crm-report/lead-details',
        params: {
          status: input.status,
          source: input.source,
          country: input.country,
          assignedUserId: input.assignedUserId,
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getProspectsEngagedButNotConverted = (
    input: ProspectsEngagedFilterDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, ProspectsEngagedReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/crm-report/prospects-engaged-but-not-converted',
        params: {
          companyId: input.companyId,
          leadAgeDays: input.leadAgeDays,
          minInteractions: input.minInteractions,
          leadId: input.leadId,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getSalesPipelineAnalytics = (
    input: SalesPipelineAnalyticsFilterDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, SalesPipelineAnalyticsReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/crm-report/sales-pipeline-analytics',
        params: {
          pipelineBy: input.pipelineBy,
          basedOn: input.basedOn,
          range: input.range,
          opportunityType: input.opportunityType,
          status: input.status,
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
        },
      },
      { apiName: this.apiName, ...config },
    );
}
