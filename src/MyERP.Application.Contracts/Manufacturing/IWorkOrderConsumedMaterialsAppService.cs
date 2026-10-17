using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IWorkOrderConsumedMaterialsAppService : IApplicationService
{
    Task<WorkOrderConsumedMaterialsReportDto> GetReportAsync(WorkOrderConsumedMaterialsRequestDto input);
}
