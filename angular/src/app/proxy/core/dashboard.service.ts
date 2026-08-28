import type { AgingSummaryWidgetDto, BankBalanceWidgetDto, CashFlowSnapshotDto, DashboardSummaryDto, DeliveryDueAlertDto, ExpiringBatchDto, ExpiringQuotationDto, FinancialKpiDto, LowStockItemDto, OperationalMetricsDto, OverdueAlertsDto, PendingMaterialRequestDto, PendingOrdersSummaryDto, ProductionSummaryDto, ProfitMarginTrendDto, QuickReorderDto, QuickReorderResultDto, ReorderPointDashboardDto, RevenueTrendDto, RevenueVsExpenseDto, StockValuationWidgetDto, SupplierPerformanceWidgetDto, TodaysActivityDto, TopCustomerDto, TopDebtorDto, UpcomingPaymentDuesDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DashboardService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  createReorderMaterialRequest = (input: QuickReorderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuickReorderResultDto>({
      method: 'POST',
      url: '/api/app/dashboard/reorder-material-request',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getAgingSummaryWidget = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AgingSummaryWidgetDto>({
      method: 'GET',
      url: `/api/app/dashboard/aging-summary-widget/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getBankBalances = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankBalanceWidgetDto>({
      method: 'GET',
      url: `/api/app/dashboard/bank-balances/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getCashFlowSnapshot = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CashFlowSnapshotDto>({
      method: 'GET',
      url: `/api/app/dashboard/cash-flow-snapshot/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getDeliveryDueAlerts = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryDueAlertDto>({
      method: 'GET',
      url: `/api/app/dashboard/delivery-due-alerts/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getExpiringBatches = (companyId: string, daysAhead: number = 30, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExpiringBatchDto[]>({
      method: 'GET',
      url: `/api/app/dashboard/expiring-batches/${companyId}`,
      params: { daysAhead },
    },
    { apiName: this.apiName,...config });
  

  getExpiringQuotations = (companyId: string, daysAhead: number = 7, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExpiringQuotationDto[]>({
      method: 'GET',
      url: `/api/app/dashboard/expiring-quotations/${companyId}`,
      params: { daysAhead },
    },
    { apiName: this.apiName,...config });
  

  getFinancialKpis = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FinancialKpiDto>({
      method: 'GET',
      url: `/api/app/dashboard/financial-kpis/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getLowStockItems = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LowStockItemDto[]>({
      method: 'GET',
      url: `/api/app/dashboard/low-stock-items/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getOperationalMetrics = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OperationalMetricsDto>({
      method: 'GET',
      url: `/api/app/dashboard/operational-metrics/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getOverdueAlerts = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OverdueAlertsDto>({
      method: 'GET',
      url: `/api/app/dashboard/overdue-alerts/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getPendingMaterialRequests = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PendingMaterialRequestDto[]>({
      method: 'GET',
      url: `/api/app/dashboard/pending-material-requests/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getPendingOrdersSummary = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PendingOrdersSummaryDto>({
      method: 'GET',
      url: `/api/app/dashboard/pending-orders-summary/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getProductionSummary = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionSummaryDto>({
      method: 'GET',
      url: `/api/app/dashboard/production-summary/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getProfitMarginTrend = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProfitMarginTrendDto[]>({
      method: 'GET',
      url: `/api/app/dashboard/profit-margin-trend/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getReorderPointDashboard = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReorderPointDashboardDto>({
      method: 'GET',
      url: `/api/app/dashboard/reorder-point-dashboard/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getRevenueTrend = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, RevenueTrendDto[]>({
      method: 'GET',
      url: '/api/app/dashboard/revenue-trend',
    },
    { apiName: this.apiName,...config });
  

  getRevenueVsExpenseTrend = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, RevenueVsExpenseDto[]>({
      method: 'GET',
      url: '/api/app/dashboard/revenue-vs-expense-trend',
    },
    { apiName: this.apiName,...config });
  

  getStockValuationSummary = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockValuationWidgetDto>({
      method: 'GET',
      url: `/api/app/dashboard/stock-valuation-summary/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getSummary = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DashboardSummaryDto>({
      method: 'GET',
      url: '/api/app/dashboard/summary',
    },
    { apiName: this.apiName,...config });
  

  getSupplierPerformanceWidget = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierPerformanceWidgetDto>({
      method: 'GET',
      url: `/api/app/dashboard/supplier-performance-widget/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getTodaysActivity = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TodaysActivityDto>({
      method: 'GET',
      url: `/api/app/dashboard/todays-activity/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getTopCustomers = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TopCustomerDto[]>({
      method: 'GET',
      url: `/api/app/dashboard/top-customers/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getTopDebtors = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TopDebtorDto[]>({
      method: 'GET',
      url: `/api/app/dashboard/top-debtors/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getUpcomingPaymentDues = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UpcomingPaymentDuesDto>({
      method: 'GET',
      url: `/api/app/dashboard/upcoming-payment-dues/${companyId}`,
    },
    { apiName: this.apiName,...config });
}