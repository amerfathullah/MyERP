using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IBatchWiseBalanceHistoryAppService : IApplicationService
{
    Task<BatchWiseBalanceHistoryReportDto> GetReportAsync(BatchWiseBalanceHistoryFilterDto input);
}
