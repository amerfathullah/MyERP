using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IWorkOrderSummaryAppService : IApplicationService
{
    Task<WorkOrderSummaryDto> GetSummaryAsync(WorkOrderSummaryRequestDto input);
}
