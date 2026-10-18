using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IProductionPlanningReportAppService : IApplicationService
{
    Task<ProductionPlanningReportDto> GetReportAsync(ProductionPlanningReportFilterDto input);
}
