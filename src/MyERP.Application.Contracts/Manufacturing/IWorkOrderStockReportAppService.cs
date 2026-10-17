using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IWorkOrderStockReportAppService : IApplicationService
{
    Task<WorkOrderStockReportDto> GetReportAsync(WorkOrderStockReportRequestDto input);
}
