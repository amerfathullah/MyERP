using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IProcessLossReportAppService : IApplicationService
{
    Task<ProcessLossReportDto> GetReportAsync(ProcessLossReportRequestDto input);
}
