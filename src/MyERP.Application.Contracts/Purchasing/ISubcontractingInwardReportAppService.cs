using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

/// <summary>
/// Application service for Subcontracting Inward reports (ERPNext PR #59395).
/// </summary>
public interface ISubcontractingInwardReportAppService : IApplicationService
{
    /// <summary>
    /// Gets finished goods from active subcontracting inward orders that are pending delivery to the customer.
    /// </summary>
    Task<SubcontractedItemsToBeDeliveredReportDto> GetItemsToBeDeliveredReportAsync(SubcontractingInwardReportFilterDto input);

    /// <summary>
    /// Gets summary overview of subcontracting inward orders across documents and item rows.
    /// </summary>
    Task<SubcontractingInwardOrderSummaryReportDto> GetOrderSummaryReportAsync(SubcontractingInwardReportFilterDto input);
}
