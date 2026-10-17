using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

/// <summary>
/// Delayed Item Report service.
/// Tracks per-item delivery delays comparing actual dispatch dates with expected delivery dates.
/// Ported from ERPNext stock/report/delayed_item_report.
/// </summary>
public interface IDelayedItemReportAppService : IApplicationService
{
    Task<DelayedItemReportDto> GetReportAsync(DelayedItemFilterDto input);
}
