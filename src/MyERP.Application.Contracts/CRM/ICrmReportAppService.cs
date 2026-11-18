using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.CRM;

public interface ICrmReportAppService : IApplicationService
{
    Task<LeadDetailsReportDto> GetLeadDetailsAsync(LeadDetailsFilterDto input);
    Task<LeadConversionTimeReportDto> GetLeadConversionTimeAsync(CrmReportDateFilterDto input);
    Task<ProspectsEngagedReportDto> GetProspectsEngagedButNotConvertedAsync(ProspectsEngagedFilterDto input);
    Task<SalesPipelineAnalyticsReportDto> GetSalesPipelineAnalyticsAsync(SalesPipelineAnalyticsFilterDto input);
}
