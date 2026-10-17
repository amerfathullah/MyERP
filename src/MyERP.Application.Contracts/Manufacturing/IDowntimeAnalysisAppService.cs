using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IDowntimeAnalysisAppService : IApplicationService
{
    Task<DowntimeAnalysisReportDto> GetReportAsync(DowntimeAnalysisRequestDto input);
}
