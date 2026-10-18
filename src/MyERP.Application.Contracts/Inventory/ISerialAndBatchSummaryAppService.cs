using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface ISerialAndBatchSummaryAppService : IApplicationService
{
    Task<SerialAndBatchSummaryReportDto> GetReportAsync(SerialAndBatchSummaryFilterDto input);
}
