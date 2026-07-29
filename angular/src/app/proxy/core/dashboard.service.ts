import type { AgingSummaryWidgetDto, DashboardSummaryDto, FinancialKpiDto, LowStockItemDto, OperationalMetricsDto, RevenueTrendDto, StockValuationWidgetDto } from './models';
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
  

  getStockValuationSummary = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockValuationWidgetDto>({
      method: 'GET',
      url: `/api/app/dashboard/stock-valuation-summary/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getOverdueAlerts = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any>({
      method: 'GET',
      url: `/api/app/dashboard/overdue-alerts/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getAgingSummaryWidget = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AgingSummaryWidgetDto>({
      method: 'GET',
      url: `/api/app/dashboard/aging-summary-widget/${companyId}`,
    },
    { apiName: this.apiName,...config });

  getCashFlowSnapshot = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any>({
      method: 'GET',
      url: `/api/app/dashboard/cash-flow-snapshot/${companyId}`,
    },
    { apiName: this.apiName,...config });

  getBankBalances = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any>({
      method: 'GET',
      url: `/api/app/dashboard/bank-balances/${companyId}`,
    },
    { apiName: this.apiName,...config });

  getSummary = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DashboardSummaryDto>({
      method: 'GET',
      url: '/api/app/dashboard/summary',
    },
    { apiName: this.apiName,...config });

  getExpiringQuotations = (companyId: string, daysAhead: number = 7, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any[]>({
      method: 'GET',
      url: `/api/app/dashboard/expiring-quotations/${companyId}`,
      params: { daysAhead },
    },
    { apiName: this.apiName,...config });

  getTopCustomers = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any[]>({
      method: 'GET',
      url: `/api/app/dashboard/top-customers/${companyId}`,
    },
    { apiName: this.apiName,...config });

  getPendingOrdersSummary = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any>({
      method: 'GET',
      url: `/api/app/dashboard/pending-orders-summary/${companyId}`,
    },
    { apiName: this.apiName,...config });

  getProductionSummary = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any>({
      method: 'GET',
      url: `/api/app/dashboard/production-summary/${companyId}`,
    },
    { apiName: this.apiName,...config });

  getExpiringBatches = (companyId: string, daysAhead: number = 30, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any[]>({
      method: 'GET',
      url: `/api/app/dashboard/expiring-batches/${companyId}`,
      params: { daysAhead },
    },
    { apiName: this.apiName,...config });

  getTopDebtors = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any[]>({
      method: 'GET',
      url: `/api/app/dashboard/top-debtors/${companyId}`,
    },
    { apiName: this.apiName,...config });
}