using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

/// <summary>
/// Application service contract for Sales Invoice Trends report.
/// Maps to ERPNext accounts/report/sales_invoice_trends.
/// </summary>
public interface ISalesInvoiceTrendsAppService : IApplicationService
{
    Task<SalesInvoiceTrendsReportDto> GetReportAsync(SalesInvoiceTrendsRequestDto input);
}
