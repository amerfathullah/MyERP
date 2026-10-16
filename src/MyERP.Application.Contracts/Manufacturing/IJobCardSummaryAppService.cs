using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IJobCardSummaryAppService : IApplicationService
{
    Task<JobCardSummaryDto> GetSummaryAsync(JobCardSummaryRequestDto input);
}
