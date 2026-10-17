using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

/// <summary>
/// Stock Analytics Report service.
/// Tracks periodic stock balance and valuation trends over Monthly/Quarterly/Half-Yearly/Yearly periods.
/// Ported from ERPNext stock/report/stock_analytics.
/// </summary>
public interface IStockAnalyticsAppService : IApplicationService
{
    Task<StockAnalyticsReportDto> GetReportAsync(StockAnalyticsRequestDto input);
}
