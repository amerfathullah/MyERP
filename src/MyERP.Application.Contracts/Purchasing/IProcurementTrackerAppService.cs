using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IProcurementTrackerAppService : IApplicationService
{
    Task<ProcurementTrackerReportDto> GetReportAsync(ProcurementTrackerFilterDto input);
}
