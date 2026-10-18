using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface ILandedCostReportAppService : IApplicationService
{
    Task<LandedCostReportDto> GetReportAsync(LandedCostReportFilterDto input);
}
