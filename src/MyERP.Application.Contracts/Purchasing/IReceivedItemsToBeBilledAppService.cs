using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

/// <summary>
/// Application service contract for Received Items to be Billed report.
/// Maps to ERPNext accounts/report/received_items_to_be_billed.
/// </summary>
public interface IReceivedItemsToBeBilledAppService : IApplicationService
{
    Task<ReceivedItemsToBeBilledReportDto> GetReportAsync(GetReceivedItemsToBeBilledInput input);
}
