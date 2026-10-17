using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface ITotalStockSummaryAppService : IApplicationService
{
    Task<TotalStockSummaryReportDto> GetReportAsync(TotalStockSummaryFilterDto input);
}
