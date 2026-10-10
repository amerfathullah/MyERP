using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.CRM;

/// <summary>
/// Campaign Efficiency Report Service.
/// Maps to ERPNext crm/report/campaign_efficiency / PR #59916.
/// </summary>
public interface ICampaignEfficiencyAppService : IApplicationService
{
    Task<CampaignEfficiencyReportDto> GetReportAsync(CampaignEfficiencyFilterDto filter);
}
