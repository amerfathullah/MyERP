using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IAvailableBatchReportAppService : IApplicationService
{
    Task<AvailableBatchReportDto> GetReportAsync(AvailableBatchFilterDto input);
}
