using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface ICostOfPoorQualityReportAppService : IApplicationService
{
    Task<CostOfPoorQualityReportDto> GetReportAsync(CostOfPoorQualityReportRequestDto input);
}
