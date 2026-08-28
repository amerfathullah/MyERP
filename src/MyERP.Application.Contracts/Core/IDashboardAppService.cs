using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IDashboardAppService : IApplicationService
{
    Task<DashboardSummaryDto> GetSummaryAsync();
    Task<List<LowStockItemDto>> GetLowStockItemsAsync(Guid companyId);
    Task<QuickReorderResultDto> CreateReorderMaterialRequestAsync(QuickReorderDto input);
    Task<List<RevenueTrendDto>> GetRevenueTrendAsync();
    Task<List<RevenueVsExpenseDto>> GetRevenueVsExpenseTrendAsync();
    Task<FinancialKpiDto> GetFinancialKpisAsync(Guid companyId);
    Task<OperationalMetricsDto> GetOperationalMetricsAsync(Guid companyId);
    Task<StockValuationWidgetDto> GetStockValuationSummaryAsync(Guid companyId);
    Task<OverdueAlertsDto> GetOverdueAlertsAsync(Guid companyId);
    Task<TodaysActivityDto> GetTodaysActivityAsync(Guid companyId);
    Task<List<PendingMaterialRequestDto>> GetPendingMaterialRequestsAsync(Guid companyId);
    Task<BankBalanceWidgetDto> GetBankBalancesAsync(Guid companyId);
    Task<AgingSummaryWidgetDto> GetAgingSummaryWidgetAsync(Guid companyId);
    Task<CashFlowSnapshotDto> GetCashFlowSnapshotAsync(Guid companyId);
    Task<List<ExpiringQuotationDto>> GetExpiringQuotationsAsync(Guid companyId, int daysAhead = 7);
    Task<List<TopCustomerDto>> GetTopCustomersAsync(Guid companyId);
    Task<PendingOrdersSummaryDto> GetPendingOrdersSummaryAsync(Guid companyId);
    Task<ProductionSummaryDto> GetProductionSummaryAsync(Guid companyId);
    Task<List<ExpiringBatchDto>> GetExpiringBatchesAsync(Guid companyId, int daysAhead = 30);
    Task<List<TopDebtorDto>> GetTopDebtorsAsync(Guid companyId);
    Task<UpcomingPaymentDuesDto> GetUpcomingPaymentDuesAsync(Guid companyId);
    Task<List<ProfitMarginTrendDto>> GetProfitMarginTrendAsync(Guid companyId);
    Task<DeliveryDueAlertDto> GetDeliveryDueAlertsAsync(Guid companyId);
    Task<ReorderPointDashboardDto> GetReorderPointDashboardAsync(Guid companyId);
    Task<SupplierPerformanceWidgetDto> GetSupplierPerformanceWidgetAsync(Guid companyId);
}
