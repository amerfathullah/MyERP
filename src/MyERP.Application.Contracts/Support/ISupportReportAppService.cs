using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Support;

public interface ISupportReportAppService : IApplicationService
{
    Task<IssueSummaryReportDto> GetIssueSummaryAsync(SupportReportFilterDto input);
    Task<IssueAnalyticsReportDto> GetIssueAnalyticsAsync(SupportReportFilterDto input);
    Task<FirstResponseTimeReportDto> GetFirstResponseTimeAsync(SupportReportFilterDto input);
    Task<SupportHourDistributionReportDto> GetSupportHourDistributionAsync(SupportReportFilterDto input);
}
