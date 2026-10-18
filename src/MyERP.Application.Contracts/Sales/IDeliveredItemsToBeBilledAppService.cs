using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

/// <summary>
/// Application service contract for Delivered Items to be Billed report.
/// Maps to ERPNext accounts/report/delivered_items_to_be_billed.
/// </summary>
public interface IDeliveredItemsToBeBilledAppService : IApplicationService
{
    Task<DeliveredItemsToBeBilledReportDto> GetReportAsync(GetDeliveredItemsToBeBilledInput input);
}
