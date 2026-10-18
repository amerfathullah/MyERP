using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface ISerialNoAndBatchTraceabilityAppService : IApplicationService
{
    Task<SerialNoAndBatchTraceabilityReportDto> GetReportAsync(SerialNoAndBatchTraceabilityFilterDto input);
}
