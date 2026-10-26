using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ITerritoryTargetVarianceAppService : IApplicationService
{
    Task<TerritoryTargetVarianceReportDto> GetReportAsync(GetTerritoryTargetVarianceInput input);
}
