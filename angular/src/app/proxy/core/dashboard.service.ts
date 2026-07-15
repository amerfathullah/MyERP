import type { DashboardSummaryDto, FinancialKpiDto, LowStockItemDto, OperationalMetricsDto, RevenueTrendDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DashboardService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getFinancialKpis = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FinancialKpiDto>({
      method: 'GET',
      url: `/api/app/dashboard/financial-kpis/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getLowStockItems = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, LowStockItemDto[]>({
      method: 'GET',
      url: '/api/app/dashboard/low-stock-items',
    },
    { apiName: this.apiName,...config });
  

  getOperationalMetrics = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OperationalMetricsDto>({
      method: 'GET',
      url: `/api/app/dashboard/operational-metrics/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getRevenueTrend = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, RevenueTrendDto[]>({
      method: 'GET',
      url: '/api/app/dashboard/revenue-trend',
    },
    { apiName: this.apiName,...config });
  

  getSummary = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DashboardSummaryDto>({
      method: 'GET',
      url: '/api/app/dashboard/summary',
    },
    { apiName: this.apiName,...config });
}