using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IQualityInspectionSummaryAppService : IApplicationService
{
    Task<QualityInspectionSummaryReportDto> GetReportAsync(QualityInspectionSummaryRequestDto input);
}
