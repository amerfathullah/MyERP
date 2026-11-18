import type {
  FirstResponseTimeReportDto,
  IssueAnalyticsReportDto,
  IssueSummaryReportDto,
  SupportHourDistributionReportDto,
  SupportReportFilterDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SupportReportService {
  private restService = inject(RestService);
  apiName = 'Default';

  getFirstResponseTime = (input: SupportReportFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FirstResponseTimeReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/support-report/first-response-time',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          basedOn: input.basedOn,
          range: input.range,
          status: input.status,
          priority: input.priority,
          customerId: input.customerId,
          assignedToId: input.assignedToId,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getIssueAnalytics = (input: SupportReportFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssueAnalyticsReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/support-report/issue-analytics',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          basedOn: input.basedOn,
          range: input.range,
          status: input.status,
          priority: input.priority,
          customerId: input.customerId,
          assignedToId: input.assignedToId,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getIssueSummary = (input: SupportReportFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssueSummaryReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/support-report/issue-summary',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          basedOn: input.basedOn,
          range: input.range,
          status: input.status,
          priority: input.priority,
          customerId: input.customerId,
          assignedToId: input.assignedToId,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getSupportHourDistribution = (input: SupportReportFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupportHourDistributionReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/support-report/support-hour-distribution',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          basedOn: input.basedOn,
          range: input.range,
          status: input.status,
          priority: input.priority,
          customerId: input.customerId,
          assignedToId: input.assignedToId,
        },
      },
      { apiName: this.apiName, ...config },
    );
}
